using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using Microsoft.Win32;
using Serilog;
using uppm.Core.Repositories;

namespace uppm.Core
{
    /// <summary>
    /// Machine type of an executable
    /// </summary>
    public enum MachineType
    {
        Native = 0,
        x86 = 0x014c,
        Itanium = 0x0200,
        x64 = 0x8664
    }

    // TODO: refactor so packages tell their target applications
    /// <summary>
    /// Packages can target any associated application it manages packages for.
    /// This class contains information about such a target application
    /// </summary>
    public abstract class TargetApp : ILogging
    {
        private static readonly Dictionary<string, TargetApp> _knownTargetApps = new Dictionary<string, TargetApp>();
        
        /// <summary>
        /// The globally inferred target application, unless a package specifies a different one
        /// </summary>
        public static TargetApp CurrentTargetApp { get; private set; }

        /// <summary>
        /// Tries to get a known target application
        /// </summary>
        /// <param name="sname"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public static bool TryGetKnownApp(string sname, out TargetApp app) => _knownTargetApps.TryGetValue(sname, out app);

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
            _knownTargetApps.UpdateGeneric(ShortName, this);
            return this;
        }

        public TargetApp SetAsCurrentApp()
        {
            CurrentTargetApp?.DefaultRepository?.UnregisterDefaultRepository();
            DefaultRepository.RegisterRepository();
            _knownTargetApps.UpdateGeneric(ShortName, this);
            CurrentTargetApp = this;
            return this;
        }

        /// <summary>
        /// Gets the processor architecture or the machine type of the target application.
        /// </summary>
        /// <returns></returns>
        public virtual MachineType GetArchitecture()
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
            return (MachineType)machineUint;
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

    /// <summary>
    /// Target application is vvvv
    /// </summary>
    public class VvvvApp : TargetApp
    {
        /// <inheritdoc />
        public override string ShortName { get => "vvvv"; set { } }

        /// <inheritdoc />
        public override string Executable { get => "vvvv.exe"; set { } }

        /// <inheritdoc />
        public override string AppFolder
        {
            get => PAppFolder;
            set => PAppFolder = GetVvvvFolder(value);
        }

        /// <inheritdoc />
        public override string GlobalPacksFolder
        {
            get => PAppFolder;
            set { }
        }

        /// <inheritdoc />
        public override string LocalPacksFolder
        {
            get => PAppFolder;
            set { }
        }

        private string GetVvvvFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                var regkey = (string)Registry.GetValue("HKEY_CLASSES_ROOT\\VVVV\\Shell\\Open\\Command", "", "");
                if (string.IsNullOrWhiteSpace(regkey))
                {
                    Log.Information("There's no VVVV registered, using working directory", this);
                    return Environment.CurrentDirectory;
                }
                else
                {
                    var exepath = regkey.Split(' ')[0].Replace("\"", "");
                    //Console.WriteLine("Found a VVVV in registry.");
                    return Path.GetDirectoryName(Path.GetFullPath(exepath));
                }
            }
            return CurrentOrNewFolder(dir);
        }
    }
}
