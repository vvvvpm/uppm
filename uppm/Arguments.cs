using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerArgs;
using Serilog.Events;
using uppm.Core;

namespace uppm
{
    public class UppmArguments
    {
        [ArgDescription("Short name of the application targeted by the input package.")]
        [ArgPosition(0)]
        [ArgRequired]
        public string TargetApplication { get; set; }

        [ArgDescription("Execute this command of the package")]
        [ArgPosition(1)]
        public string Command { get; set; }

        [ArgDescription("The desired package in uppm package reference format")]
        [ArgPosition(2)]
        public string Package { get; set; }

        [ArgDescription("Override the default application directory")]
        [ArgShortcut("-appdir")]
        public string AppDirOverride { get; set; }

        [ArgDescription("Override the current working directory")]
        [ArgShortcut("-workdir")]
        public string WorkDirOverride { get; set; }

        [ArgDescription("The defualt architecture of target application when it can't be determined (x86 or x64). On unattended mode x64 is assumed")]
        [ArgShortcut("-arch")]
        [ArgRegex(@"^x(64|86)(\s|$)", "Only x64 or x86 is supported as default application architecture")]
        public string AppArchitecture { get; set; }

        [ArgDescription("Uppm should not expect user input. This can lead to more exceptions or assumptions when ambiguous situation occurs")]
        [ArgShortcut("-u")]
        public bool Unattended { get; set; }

        [ArgDescription("Uppm breaks or pauses on error when it's not running unattended. Set this switch if uppm should not break.")]
        public bool ContinueOnError { get; set; }

        [ArgDescription("Only errors are displayed. This is only supported in Unattended mode (-u)")]
        [ArgShortcut("-q")]
        public bool Quiet { get; set; }

        [ArgDescription("Debug messages will be shown")]
        [ArgShortcut("-d")]
        public bool Debug { get; set; }

        [ArgDescription("Verbose and Debug messages will be shown")]
        [ArgShortcut("-v")]
        public bool Verbose { get; set; }

        public LogEventLevel GetLoggingLevel()
        {
            if (Verbose) return LogEventLevel.Verbose;
            if (Debug) return LogEventLevel.Debug;
            if (Quiet) return LogEventLevel.Error;
            return LogEventLevel.Information;
        }
    }
}
