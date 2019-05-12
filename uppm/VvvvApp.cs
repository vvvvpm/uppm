using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using uppm.Core;

namespace uppm
{
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
