using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dotnet.Script.Core;
using Dotnet.Script.DependencyModel.Environment;
using Dotnet.Script.DependencyModel.Logging;
using Dotnet.Script.DependencyModel.Runtime;
using Hjson;
using Humanizer;
using md.stdl.Coding;
using md.stdl.String;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
using Serilog;
using uppm.Core.Repositories;
using uppm.Core.Utils;

namespace uppm.Core.Scripting
{
    /// <summary>
    /// Script engine running C# 7.3
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
        public bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion, CompletePackageReference packref)
        {
            return this.TryGetCommonHjsonMeta(
                text,
                @"\/\*", @"\*\/",
                ref packmeta,
                out requiredVersion,
                packref, packref.RepositoryUrl);
        }

        private int _getScriptTextRecursionDepthCounter;
        private const int _getScriptTextRecursionMaxDepth = 500;

        /// <inheritdoc />
        public bool TryGetScriptText(string text, out string scripttext, HashSet<PartialPackageReference> imports = null, string parentRepo = "")
        {
            _getScriptTextRecursionDepthCounter++;
            //TODO: process and transform uppm-ref #loads
            // to do that create a temporary file for referenced packages
            // and inject those files into uppm-ref #loads

            var importrefsregex = new Regex("#load\\s\"uppm-ref:(?<packref>.*)\"");

            var success = true;

            var outtext = importrefsregex.Replace(text, match =>
            {
                var packreftext = match.Groups["packref"].Value;

                Log.Debug("Importing script of {PackRef}", packreftext);

                var packref = PackageReference.Parse(packreftext);

                if (!string.IsNullOrWhiteSpace(parentRepo) && !string.IsNullOrWhiteSpace(packref.RepositoryUrl))
                    packref.RepositoryUrl = parentRepo;

                success = packref.TryGetRepository(out var importPackRepo);
                if (!success)
                {
                    Log.Error("Couldn't infer repository for {PackRef} while importing its script.", packreftext);
                    return "";
                }

                Log.Verbose("    at repository {RepoUrl}", importPackRepo.Url);

                success = success &&
                          importPackRepo.TryGetScriptEngine(packref, out var importPackScriptEngine) &&
                          importPackScriptEngine is CSharpScriptEngine importCsSe;
                if (!success)
                {
                    Log.Error("{PackRef} doesn't appear to be a C# script", packreftext);
                    return "";
                }

                var importScriptText = "";
                success = success &&
                          importPackRepo.TryGetPackageText(packref, out var importPackText) &&
                          TryGetScriptText(importPackText, out importScriptText, null, parentRepo);
                if (!success)
                {
                    Log.Error("Couldn't get the script text of {PackRef}", packreftext);
                    return "";
                }

                try
                {
                    var filepath = Path.Combine(
                        Uppm.Implementation.TemporaryFolder,
                        "CSharpEngine",
                        packref.ToString().Dehumanize().Kebaberize() + ".csx"
                    );

                    File.WriteAllText(filepath, importScriptText);
                    Log.Verbose("    saved successfully at {FilePath}", filepath);
                    return $"#load \"{filepath}\"";
                }
                catch (Exception e)
                {
                    success = false;
                    Log.Error(e, "Error during importing script of {PackRef}", packref);
                    return "";
                }
            });

            _getScriptTextRecursionDepthCounter--;

            scripttext = outtext;
            return success;
        }

        /// <inheritdoc />
        public bool RunAction(Package pack, string action)
        {
            bool success = true;

            try
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

                var scriptResultT = compctx.Script.RunAsync(host, exception =>
                {
                    Log.Error(exception, "Script of {$PackRef} threw an exception:", pack.Meta.Self);
                    success = false;
                    return true;
                });
                scriptResultT.Wait();
                var scriptResult = scriptResultT.Result;

                var result = scriptResult.ReturnValue;

                if (result != null)
                {
                    if (!(result.Get(action) is Action commandDelegate))
                    {
                        Log.Error("Script of {$PackRef} doesn't contain action {ScriptAction}", pack.Meta.Self, action);
                        success = false;
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
                            success = false;
                        }
                    }
                }
                else
                {
                    Log.Error("Script of {$PackRef} didn't return an object", pack.Meta.Self);
                    success = false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error occurred during setting up the script at {$PackRef}", pack.Meta.Self);
                success = false;
            }
            return success;
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
