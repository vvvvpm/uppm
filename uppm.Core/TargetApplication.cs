using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Win32;

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
    public class TargetApplication : ILogSource
    {
        private string _folder;

        /// <summary>
        /// Short, friendly name of the application (like "vvvv")
        /// </summary>
        public virtual string ShortName { get; set; }

        /// <summary>
        /// Containing folder of the application
        /// </summary>
        public virtual string Folder
        {
            get => _folder;
            set => _folder = FolderValidation(value);
        }

        /// <summary>
        /// Executable filename without path
        /// </summary>
        public virtual string Exe { get; set; }

        /// <summary>
        /// Full path to executable
        /// </summary>
        public string AbsoluteExe => Path.GetFullPath(Path.Combine(Folder, Exe));

        /// <summary>
        /// Folder validation or inference function. If the assigned function returns null or empty string,
        /// validation is not successful and this target application cannot be managed by uppm
        /// </summary>
        public virtual Func<string, string> FolderValidation { get; set; } = dir =>
        {
            if (string.IsNullOrWhiteSpace(dir)) return Environment.CurrentDirectory;
            var adir = Path.GetFullPath(dir);
            if (Directory.Exists(adir)) return adir;
            else
            {
                Directory.CreateDirectory(adir);
                return adir;
            }
        };

        /// <summary>
        /// Gets the processor architecture or the machine type of the target application 
        /// </summary>
        /// <returns></returns>
        public MachineType GetArchitecture()
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
        public Version GetVersion()
        {
            var vinfo = FileVersionInfo.GetVersionInfo(AbsoluteExe);
            return new Version(vinfo.FileMajorPart, vinfo.FileMinorPart, vinfo.FileBuildPart);
        }

        /// <inheritdoc />
        public ILog Log { get; set; }
    }

    public class VvvvApplication : TargetApplication
    {
        public override string ShortName { get => "vvvv"; set { } }

        public override string Exe { get => "vvvv.exe"; set { } }

        public override Func<string, string> FolderValidation
        {
            get => dir =>
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    var regkey = (string)Registry.GetValue("HKEY_CLASSES_ROOT\\VVVV\\Shell\\Open\\Command", "", "");
                    if (string.IsNullOrWhiteSpace(regkey))
                    {
                        Log.UInfo("There's no VVVV registered, using working directory", this);
                        return Environment.CurrentDirectory;
                    }
                    else
                    {
                        var exepath = regkey.Split(' ')[0].Replace("\"", "");
                        //Console.WriteLine("Found a VVVV in registry.");
                        return Path.GetDirectoryName(Path.GetFullPath(exepath));
                    }
                }
                return base.FolderValidation(dir);
            };
            set { }
        }
    }
}
