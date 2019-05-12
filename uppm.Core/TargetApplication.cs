using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using md.stdl.Coding;
using Microsoft.Win32;
using Serilog;
using uppm.Core.Repositories;
using uppm.Core.Scripting;

namespace uppm.Core
{
    /// <summary>
    /// Machine type of an executable
    /// </summary>
    public enum Architecture
    {
        Native = 0,
        x86 = 0x014c,
        Itanium = 0x0200,
        x64 = 0x8664
    }

    // TODO: Applications report their packages
    /// <summary>
    /// Packages can target any associated application it manages packages for.
    /// This class contains information about such a target application
    /// </summary>
    public abstract class TargetApp : ILogging
    {
        public static readonly Dictionary<string, TargetApp> KnownTargetApps = new Dictionary<string, TargetApp>();
        private static TargetApp _currentTargetApp;

        public static readonly Architecture[] SupportedArchitectures = {Architecture.x64, Architecture.x86};

        /// <summary>
        /// The globally inferred target application, unless a package specifies a different one
        /// </summary>
        public static TargetApp CurrentTargetApp
        {
            get => _currentTargetApp;
            private set
            {
                if (value == null)
                {
                    Logging.L.Error("Trying to set current target application to null. That's nasty. Don't do that.");
                    return;
                }
                value.Log.Verbose("Set {TargetApp} as current target application", value.ShortName);
                _currentTargetApp = value;
            }
        }

        /// <summary>
        /// Tries to get a known target application
        /// </summary>
        /// <param name="sname"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public static bool TryGetKnownApp(string sname, out TargetApp app) => KnownTargetApps.TryGetValue(sname, out app);


        /// <summary>
        /// Register known target applications from an assembly
        /// </summary>
        /// <param name="assembly"></param>
        public static void RegisterApps(Assembly assembly)
        {
            var enginetypes = assembly.GetTypes().Where(
                t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(TargetApp))
            );
            foreach (var enginetype in enginetypes)
            {
                var engine = enginetype.CreateInstance() as TargetApp;
                engine?.RegisterAsKnownApp();
            }
        }

        /// <summary>
        /// Tries to set the current target application to the specified one.
        /// </summary>
        /// <param name="sname"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public static bool TrySetCurrentApp(string sname, out TargetApp app)
        {
            if (!TryGetKnownApp(sname, out app)) return false;
            CurrentTargetApp?.DefaultRepository?.UnregisterDefaultRepository();
            CurrentTargetApp = app;
            app.DefaultRepository.RegisterDefaultRepository();
            return true;
        }
        
        protected string PAppFolder;
        protected string PGlobalPacksFolder;
        protected string PLocalPacksFolder;

        /// <summary>
        /// Short, friendly name of the application (like "ue4" or "vvvv")
        /// </summary>
        public virtual string ShortName { get; set; }

        /// <summary>
        /// Desired architecture if it can't be determined from the target
        /// </summary>
        public Architecture DefualtArchitecture { get; set; } = Architecture.x64;

        public Architecture _appArch = Architecture.Native;

        public Architecture AppArchitecture
        {
            get
            {
                if (File.Exists(AbsoluteExe))
                {
                    if (_appArch == Architecture.Native)
                    {
                        _appArch = GetArchitecture();
                        Log.Verbose("Determining architecture of {TargetApp} for the first time ({Arch})", ShortName, _appArch);
                    }
                }
                else _appArch = DefualtArchitecture;

                if (_appArch == Architecture.Native)
                {
                    _appArch = Logging.AskUserEnum(
                        $"Default architecture for {ShortName} was not specified and it can't be automatically determined. Please choose one:",
                        SupportedArchitectures,
                        Architecture.x64);
                }
                return _appArch;
            }
        }

        /// <summary>
        /// Containing folder of the application. Override setter for validation and inference.
        /// </summary>
        public virtual string AppFolder
        {
            get => PAppFolder;
            set => PAppFolder = CurrentOrNewFolder(value);
        }

        /// <summary>
        /// Folder for the packs installed at global scope. Override setter for validation and inference.
        /// </summary>
        public virtual string GlobalPacksFolder
        {
            get => PGlobalPacksFolder;
            set => PGlobalPacksFolder = CurrentOrNewFolder(value);
        }

        /// <summary>
        /// Folder for the packs installed at local scope. Override setter for validation and inference.
        /// </summary>
        public virtual string LocalPacksFolder
        {
            get => PLocalPacksFolder;
            set => PLocalPacksFolder = CurrentOrNewFolder(value);
        }

        /// <summary>
        /// Executable filename without path
        /// </summary>
        public virtual string Executable { get; set; }

        /// <summary>
        /// The default package repository for this application
        /// </summary>
        public virtual IPackageRepository DefaultRepository { get; }

        /// <summary>
        /// Full path to executable
        /// </summary>
        public string AbsoluteExe => Path.GetFullPath(Path.Combine(AppFolder, Executable));

        /// <summary>
        /// If input is empty then return current directory, else return the absolute
        /// path to the potentially relative directory. If given directory doesn't exist
        /// create it.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns>Absolute path</returns>
        protected string CurrentOrNewFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return Environment.CurrentDirectory;
            var adir = Path.GetFullPath(dir);
            if (Directory.Exists(adir)) return adir;
            else
            {
                Directory.CreateDirectory(adir);
                return adir;
            }
        }

        /// <summary>
        /// Registers this target application as a known one so packages can use it.
        /// </summary>
        /// <returns></returns>
        public TargetApp RegisterAsKnownApp()
        {
            DefaultRepository.RegisterRepository();
            KnownTargetApps.UpdateGeneric(ShortName, this);
            Log.Verbose("Registering {TargetApp} as known target application", ShortName);
            return this;
        }

        public TargetApp SetAsCurrentApp()
        {
            CurrentTargetApp?.DefaultRepository?.UnregisterDefaultRepository();
            DefaultRepository.RegisterRepository();
            KnownTargetApps.UpdateGeneric(ShortName, this);
            CurrentTargetApp = this;
            return this;
        }

        /// <summary>
        /// Gets the processor architecture or the machine type of the target application.
        /// </summary>
        /// <returns></returns>
        protected virtual Architecture GetArchitecture()
        {
            const int PE_POINTER_OFFSET = 60;
            const int MACHINE_OFFSET = 4;
            byte[] data = new byte[4096];
            using (Stream s = new FileStream(AbsoluteExe, FileMode.Open, FileAccess.Read))
            {
                s.Read(data, 0, 4096);
            }
            // dos header is 64 bytes, last element, long (4 bytes) is the address of the PE header
            int PE_HEADER_ADDR = BitConverter.ToInt32(data, PE_POINTER_OFFSET);
            int machineUint = BitConverter.ToUInt16(data, PE_HEADER_ADDR + MACHINE_OFFSET);
            return (Architecture)machineUint;
        }

        /// <summary>
        /// Gets the file version of the target application
        /// </summary>
        /// <returns></returns>
        public virtual UppmVersion GetVersion()
        {
            var vinfo = FileVersionInfo.GetVersionInfo(AbsoluteExe);
            return new UppmVersion(vinfo.FileMajorPart, vinfo.FileMinorPart, vinfo.FileBuildPart);
        }

        public TargetApp()
        {
            Log = this.GetContext();
        }

        /// <inheritdoc />
        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);
    }

    /// <summary>
    /// In case the current package manager is the target application
    /// </summary>
    public class CurrentUppmApp : TargetApp
    {
        /// <inheritdoc />
        public override string AppFolder
        {
            get => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            set { }
        }

        /// <inheritdoc />
        public override string GlobalPacksFolder
        {
            get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Uppm.Implementation.GetSystemName());
            set { }
        }

        /// <inheritdoc />
        public override string ShortName => Uppm.Implementation.GetSystemName();
    }
}
