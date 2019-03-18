using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Serilog;

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

    /// <summary>
    /// Uppm can target any associated application it manages packages for.
    /// This class contains information about such a target application
    /// </summary>
    public abstract class TargetApplication : ILogSource
    {
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
        /// Full path to executable
        /// </summary>
        public string AbsoluteExe => Path.GetFullPath(Path.Combine(AppFolder, Executable));

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

        /// <inheritdoc />
        public ILogger Log { get; set; }
    }

    /// <summary>
    /// Target application is vvvv
    /// </summary>
    public class VvvvApplication : TargetApplication
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
