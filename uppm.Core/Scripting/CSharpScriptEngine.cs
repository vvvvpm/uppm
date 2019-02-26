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
using log4net;
using md.stdl.Coding;
using md.stdl.String;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
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
                    break;
                case LogLevel.Debug:
                    source.Log.UDebug(message, source);
                    break;
                case LogLevel.Info:
                    source.Log.UInfo(message, source);
                    break;
                case LogLevel.Warning:
                    source.Log.UWarn(message, source, exception);
                    break;
                case LogLevel.Error:
                    source.Log.UError(message, source, exception);
                    break;
                case LogLevel.Critical:
                    source.Log.UFatal(message, source, exception);
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
        public bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion)
        {
            var matches = text.MatchGroup(
                @"\A\/\*\s+uppm\s+(?<pmversion>[\d\.]+)\s+(?<packmeta>\{.*\})\s+\*\/",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

            packmeta = packmeta ?? new PackageMeta();
            requiredVersion = new VersionRequirement();

            if (matches == null ||
                matches.Count == 0 ||
                matches["pmversion"]?.Length <= 0 ||
                matches["packmeta"]?.Length <= 0
            )
                return false;

            if (UppmVersion.TryParse(matches["pmversion"].Value, out var minuppmversion, UppmVersion.Inference.Zero))
            {
                requiredVersion.MinimalVersion = minuppmversion;
                requiredVersion.Valid = minuppmversion <= UppmVersion.CoreVersion;
                var metatext = matches["packmeta"].Value;
                try
                {
                    PackageMeta.ParseFromHjson(metatext, ref packmeta);
                }
                catch (Exception e)
                {
                    requiredVersion.Valid = false;
                }
                return requiredVersion.Valid;
            }
            else return false;
        }

        /// <inheritdoc />
        public string GetScriptText(string text)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async void RunAction(Package pack, string action)
        {
            var logger = CreateScriptLogger(this);
            var src = SourceText.From(pack.Meta.Text);
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
                Log.UError(exception.Message, host, exception);
                return true;
            }).ConfigureAwait(false);
            
            var result = scriptResult.ReturnValue;

            if (result != null)
            {
                if (!(result.Get(action) is Action commandDelegate))
                {
                    Log.UError("Package doesn't contain specified action", this);
                }
                else
                {
                    commandDelegate();
                    //TODO exception handling
                }
            }
            else
            {
                
            }
        }

        /// <inheritdoc />
        public ILog Log { get; set; }
    }
}
