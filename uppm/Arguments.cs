using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uppm.Core;

namespace uppm
{
    public class Arguments
    {
        public List<string> Packages { get; } = new List<string>();

        public TargetApp Application { get; set; }

        public string ApplicationMode { get; set; } =
            "vvvv";

        public string DefaultArchitecture { get; set; }

        public List<string> PackRepositories { get; } =
            new List<string> { "https://github.com/vvvvpm/uppm.db.vvvv.git" };

        public List<string> NugetSources { get; set; } = new List<string>() { "https://api.nuget.org/v3/index.json" };

        public bool Unattended { get; set; }

        public bool AutoUpdateApplication { get; set; }

        public bool AutoUpdateUppm { get; set; }
    }
}
