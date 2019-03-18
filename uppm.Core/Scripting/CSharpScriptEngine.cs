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
        public static LogFactory CreateScriptLogger(ILogSource source) => type => (level, message, exception) =>
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
        public IUppmImplementation Uppm { get; set; } = new UnknownUppmImplementation();

        /// <inheritdoc />
        public string Extension => "csup";

        /// <inheritdoc />
        public bool AllowSystemAssociation => true;

        /// <inheritdoc />
        public bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion, CompletePackageReference packref = null)
        {
            var matches = text.MatchGroup(
                @"\A\/\*\s+uppm\s+(?<uppmversion>[\d\.]+)\s+(?<packmeta>\{.*\})\s+\*\/",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

            packmeta = packmeta ?? new PackageMeta();
            if (packref != null) packmeta.Self = packref;
            packmeta.Text = text;

            requiredVersion = new VersionRequirement();

            if (matches == null ||
                matches.Count == 0 ||
                matches["uppmversion"]?.Length <= 0 ||
                matches["packmeta"]?.Length <= 0
            )
            {
                Log.Error("{PackRef} doesn't contain valid metadata.", packref?.ToString() ?? "Script");
                return false;
            }

            if (UppmVersion.TryParse(matches["uppmversion"].Value, out var minuppmversion, UppmVersion.Inference.Zero))
            {
                requiredVersion.MinimalVersion = minuppmversion;
                requiredVersion.Valid = minuppmversion <= UppmVersion.CoreVersion;
                packmeta.RequiredUppmVersion = requiredVersion;
                var metatext = matches["packmeta"].Value;
                try
                {
                    PackageMeta.ParseFromHjson(metatext, ref packmeta);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Parsing metadata of {PackRef} threw an exception.", packref?.ToString() ?? "script");
                    requiredVersion.Valid = false;
                    return false;
                }

                if (requiredVersion.Valid)
                {
                    if (TryGetScriptText(text, out var scripttext, packmeta.Imports))
                    {
                        packmeta.ScriptText = scripttext;
                        return true;
                    }
                    else
                    {
                        Log.Error("Couldn't get the script text of {PackRef}.", packref?.ToString() ?? packmeta.Name);
                        return false;
                    }
                }
                else
                {
                    Log.Error(
                        "{PackRef} requires at least uppm {$RequiredMinVersion}. " +
                        "It's incompatible with Current version of uppm ({$UppmVersion})",
                        packref?.ToString() ?? packmeta.Name,
                        requiredVersion.MinimalVersion,
                        UppmVersion.CoreVersion);
                    return false;
                }
            }
            {
                Log.Error("{PackRef} doesn't contain valid metadata.", packref?.ToString() ?? "Script");
                return false;
            }
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

            var host = new ScriptHost(pack, Uppm);

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
                    commandDelegate();
                    //TODO exception handling
                }
            }
            else
            {
                Log.Error("Script of {$PackRef} didn't return an object", pack.Meta.Self);
            }
        }

        /// <inheritdoc />
        public ILogger Log { get; }

        public CSharpScriptEngine()
        {
            Log = this.GetContext();
        }
    }
}
