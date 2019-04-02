using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using md.stdl.String;
using Serilog;

namespace uppm.Core.Scripting
{
    public class PowerShellScriptEngine : IScriptEngine
    {
        /// <inheritdoc />
        public string Extension => "ps1";

        /// <inheritdoc />
        public bool AllowSystemAssociation => false;

        /// <inheritdoc />
        public bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion, CompletePackageReference packref)
        {
            return this.TryGetCommonHjsonMeta(
                text,
                @"\<\#", @"\#\>",
                ref packmeta,
                out requiredVersion,
                packref);
        }

        /// <inheritdoc />
        public bool TryGetScriptText(string text, out string scripttext, HashSet<PartialPackageReference> imports)
        {
            scripttext = text;
            return true;
        }

        /// <inheritdoc />
        public void RunAction(Package pack, string action)
        {
            if (!action.EqualsCaseless("install"))
            {
                Log.Fatal(
                    "PowerShell script engine doesn't support other actions than `Install` at the moment\n" +
                    "    at {$PackRef}",
                    pack.Meta.Self);
                return;
            }

            var shell = PowerShell.Create().AddScript(pack.Meta.ScriptText);
            shell.Streams.Verbose.DataAdded += (sender, args) => Log.Verbose("{PsOutput}", args.ToString());
            shell.Streams.Debug.DataAdded += (sender, args) => Log.Debug("{PsOutput}", args.ToString());
            shell.Streams.Progress.DataAdded += (sender, args) => this.InvokeAnyProgress(message: args.ToString());
            shell.Streams.Warning.DataAdded += (sender, args) => Log.Warning("{PsOutput}", args.ToString());
            shell.Streams.Error.DataAdded += (sender, args) => Log.Error("{PsOutput}", args.ToString());
            foreach (var psobj in shell.Invoke())
            {
                Log.Information("{PsOutput}", psobj.ToString());
            }
            
            shell.Dispose();
        }

        /// <inheritdoc />
        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);

        public PowerShellScriptEngine()
        {
            Log = this.GetContext();
        }
    }
}
