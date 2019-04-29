using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dotnet.Script.Core;
using Dotnet.Script.DependencyModel.Environment;
using Dotnet.Script.DependencyModel.Logging;
using Dotnet.Script.DependencyModel.Runtime;
using Hjson;
using md.stdl.Coding;
using md.stdl.String;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
using Serilog;
using uppm.Core.Utils;

namespace uppm.Core.Scripting
{
    /// <summary>
    /// Script executing environment
    /// </summary>
    public class CSharpScriptEngine : IScriptEngine
    {
        private static IEnumerable<Assembly> _referencedAssemblies;

        static CSharpScriptEngine()
        {
            ScriptEnvironment.Default.OverrideTargetFramework("net461");
        }

        /// <summary>
        /// All the assemblies referenced by this project
        /// </summary>
        public static IEnumerable<Assembly> ReferencedAssemblies =>
            _referencedAssemblies ?? (
                _referencedAssemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies()
                    .Select(Assembly.Load)
                    .ToArray()
                );

        /// <summary>
        /// Create a Dotnet.Script.Core / log4net bridge
        /// </summary>
        /// <param name="source">A logging object</param>
        /// <returns></returns>
        public static LogFactory CreateScriptLogger(ILogging source) => type => (level, message, exception) =>
        {
            switch (level)
            {
                case LogLevel.Trace:
                    source.Log.Verbose(exception, message);
                    break;
                case LogLevel.Debug:
                    source.Log.Debug(exception, message);
                    break;
                case LogLevel.Info:
                    source.Log.Information(exception, message);
                    break;
                case LogLevel.Warning:
                    source.Log.Warning(exception, message);
                    break;
                case LogLevel.Error:
                    source.Log.Error(exception, message);
                    break;
                case LogLevel.Critical:
                    source.Log.Fatal(exception, message);
                    break;
            }
        };

        /// <inheritdoc />
        public string Extension => "csup";

        /// <inheritdoc />
        public bool AllowSystemAssociation => true;

        /// <inheritdoc />
        public bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion, CompletePackageReference packref = null)
        {
            return this.TryGetCommonHjsonMeta(
                text,
                @"\/\*", @"\*\/",
                ref packmeta,
                out requiredVersion,
                packref);
        }

        /// <inheritdoc />
        public bool TryGetScriptText(string text, out string scripttext, HashSet<PartialPackageReference> imports = null)
        {
            //TODO: process and transform uppm-ref #loads
            // to do that create a temporary file for referenced packages
            // and inject those files into uppm-ref #loads
            scripttext = text;
            return true;
        }

        /// <inheritdoc />
        public async void RunAction(Package pack, string action)
        {
            var logger = CreateScriptLogger(this);
            var src = SourceText.From(pack.Meta.ScriptText);
            var ctx = new ScriptContext(src, Environment.CurrentDirectory, Enumerable.Empty<string>());
            var rtdepresolver = new RuntimeDependencyResolver(logger, true);
            var compiler = new ScriptCompiler(logger, rtdepresolver);
            var compctx = compiler.CreateCompilationContext<object, ScriptHost>(ctx);

            foreach (var ass in ReferencedAssemblies)
            {
                compctx.Loader.RegisterDependency(ass);
            }

            var host = new ScriptHost(pack, Uppm.Implementation);

            var scriptResult = await compctx.Script.RunAsync(host, exception =>
            {
                Log.Error(exception, "Script of {$PackRef} threw an exception:", pack.Meta.Self);
                return true;
            }).ConfigureAwait(false);
            
            var result = scriptResult.ReturnValue;

            if (result != null)
            {
                if (!(result.Get(action) is Action commandDelegate))
                {
                    Log.Error("Script of {$PackRef} doesn't contain action {ScriptAction}", pack.Meta.Self, action);
                }
                else
                {
                    try
                    {
                        commandDelegate();
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Script of {$PackRef} thrown an unhandled exception", pack.Meta.Self);
                    }
                }
            }
            else
            {
                Log.Error("Script of {$PackRef} didn't return an object", pack.Meta.Self);
            }
        }

        /// <inheritdoc />
        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);

        public CSharpScriptEngine()
        {
            Log = this.GetContext();
        }
    }
}
