using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fasterflect;
using md.stdl.Coding;
using md.stdl.String;
using Serilog;
using uppm.Core.Utils;

namespace uppm.Core.Scripting
{
    /// <inheritdoc />
    /// <summary>
    /// When implemented represents a scripting environment. A script engine is mostly
    /// specific to a single language. Implementation has to provide execution of
    /// different actions and ways to extract the package metadata and the actual
    /// executable script text from a raw package text.
    /// </summary>
    public interface IScriptEngine : ILogging
    {
        /// <summary>
        /// File extension to be associated with this script engine, without starting dot.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Allow an application using uppm.Core to associate the file extension
        /// in the operating system associated with this script engine
        /// </summary>
        bool AllowSystemAssociation { get; }

        /// <summary>
        /// Get package metadata out of arbitrary raw package text.
        /// </summary>
        /// <param name="text">Raw contents of the package</param>
        /// <param name="packmeta">If found then the output metadata, or else null</param>
        /// <param name="requiredVersion">Information about the uppm version requirements of this pack</param>
        /// <param name="packref">Package reference for the desired pack to get the meta for</param>
        /// <returns>True if metadata is found</returns>
        bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion, CompletePackageReference packref);

        /// <summary>
        /// Tries to get the executable script text out of arbitrary raw package text.
        /// </summary>
        /// <remarks>
        /// In this method implementation must also take care of parsing and transforming
        /// script imports, if they're supported by the engine.
        /// </remarks>
        /// <param name="text">Raw contents of the package</param>
        /// <param name="scripttext">Resulting executable script text</param>
        /// <param name="imports">Optional <see cref="HashSet{T}"/> of imports found in this script</param>
        /// <param name="parentRepo">Optionally a parent repository can be specified. This is used mostly for keeping dependency contexts correct.</param>
        /// <returns>True if retrieving the script text was successful</returns>
        bool TryGetScriptText(string text, out string scripttext, HashSet<PartialPackageReference> imports = null, string parentRepo = "");

        /// <summary>
        /// Run an action identified via a string from the script of a package
        /// </summary>
        /// <param name="pack">Input package</param>
        /// <param name="action">The specified action</param>
        /// <returns>True if action ran without errors</returns>
        bool RunAction(Package pack, string action);
    }

    /// <summary>
    /// Static utilities around <see cref="IScriptEngine"/>
    /// </summary>
    public static class ScriptEngine
    {
        internal static Dictionary<string, IScriptEngine> KnownScriptEngines { get; } = new Dictionary<string, IScriptEngine>();

        static ScriptEngine()
        {
            LoadEngines(typeof(ScriptEngine).Assembly);
        }

        /// <summary>
        /// Tries to get a <see cref="IScriptEngine"/> based on its extension
        /// </summary>
        /// <param name="extension"></param>
        /// <param name="engine"></param>
        /// <returns></returns>
        public static bool TryGetEngine(string extension, out IScriptEngine engine)
        {
            return KnownScriptEngines.TryGetValue(extension, out engine);
        }

        /// <summary>
        /// Loads engines from given assembly. Implementation should call this
        /// before any package processed in case they want to load their own script engine.
        /// </summary>
        /// <param name="assembly"></param>
        public static void LoadEngines(Assembly assembly)
        {
            var enginetypes = assembly.GetTypes().Where(
                t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i == typeof(IScriptEngine))
            );
            foreach (var enginetype in enginetypes)
            {
                if(!(enginetype.CreateInstance() is IScriptEngine engine)) continue;
                KnownScriptEngines.UpdateGeneric(engine.Extension, engine);
                Logging.L.Verbose("Script engine {SciptEngine} is registered", engine.Extension);
            }
        }

        /// <summary>
        /// Try to get the meta comment text of a script
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="text">text of the entire package</param>
        /// <param name="commentStartRegexPattern">a regex pattern for comment start</param>
        /// <param name="commentEndRegexPattern">a regex pattern for comment end</param>
        /// <param name="metaText">output meta text if valid</param>
        /// <param name="requiredVersion">output version requirement if valid</param>
        /// <param name="packref">optional pack reference for logging to know which pack we're talking about.</param>
        /// <returns>True if the meta comment was found successfully</returns>
        public static bool TryGetScriptMultilineMetaComment(
            this IScriptEngine engine,
            string text,
            string commentStartRegexPattern,
            string commentEndRegexPattern,
            out string metaText,
            out VersionRequirement requiredVersion,
            CompletePackageReference packref = null)
        {
            metaText = "";
            requiredVersion = new VersionRequirement();

            var matches = text.MatchGroup(
                $@"{commentStartRegexPattern}\s+uppm\s+(?<uppmversion>[\d\.]+)\s+(?<packmeta>\{{.*\}})\s+{commentEndRegexPattern}",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

            if (matches == null ||
                matches.Count == 0 ||
                matches["uppmversion"]?.Length <= 0 ||
                matches["packmeta"]?.Length <= 0
            )
            {
                engine.Log.Error("{PackRef} doesn't contain valid metadata.", packref?.ToString() ?? "Script");
                return false;
            }

            if (UppmVersion.TryParse(matches["uppmversion"].Value, out var minuppmversion, UppmVersion.Inference.Zero))
            {
                requiredVersion.MinimalVersion = minuppmversion;
                requiredVersion.Valid = minuppmversion <= Uppm.CoreVersion;
                metaText = matches["packmeta"].Value;
                if (!requiredVersion.Valid)
                {
                    Log.Error(
                        "{PackRef} requires at least uppm {$RequiredMinVersion}. " +
                        "It's incompatible with Current version of uppm ({$UppmVersion})",
                        packref?.ToString() ?? "Script",
                        requiredVersion.MinimalVersion,
                        Uppm.CoreVersion);
                    return false;
                }
                return true;
            }
            Log.Error("{PackRef} doesn't contain valid metadata.", packref?.ToString() ?? "Script");
            return false;
        }

        /// <summary>
        /// Try to get <see cref="PackageMeta"/> out of an already extracted HJSON meta text
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="metaText">already extracted meta text in HJSON format</param>
        /// <param name="packMeta">reference to a <see cref="PackageMeta"/>.
        /// if reference is null a new one will be created and assigned to it</param>
        /// <param name="packref">optional pack reference for logging to know which pack we're talking about.</param>
        /// <param name="parentRepo">Optionally a parent repository can be specified. This is used mostly for keeping dependency contexts correct.</param>
        /// <returns>True if package metadata was parsed successfully</returns>
        public static bool TryGetHjsonScriptMultilineMeta(
            this IScriptEngine engine,
            string metaText,
            ref PackageMeta packMeta,
            CompletePackageReference packref = null,
            string parentRepo = "")
        {
            packMeta = packMeta ?? new PackageMeta();
            if (packref != null) packMeta.Self = packref;
            try
            {
                PackageMeta.ParseFromHjson(metaText, ref packMeta);
            }
            catch (Exception e)
            {
                Log.Error(e, "Parsing metadata of {PackRef} threw an exception.", packref?.ToString() ?? "script");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Common implementation for <see cref="IScriptEngine.TryGetMeta"/> in case
        /// package defines metadata as HJSON in a multiline comment
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="text">text of the entire package</param>
        /// <param name="commentStartRegexPattern">a regex pattern for comment start</param>
        /// <param name="commentEndRegexPattern">a regex pattern for comment end</param>
        /// <param name="packMeta">reference to a <see cref="PackageMeta"/>.
        /// if reference is null a new one will be created and assigned to it</param>
        /// <param name="requiredVersion">output version requirement if valid</param>
        /// <param name="packref">optional pack reference for logging to know which pack we're talking about.</param>
        /// <param name="parentRepo">Optionally a parent repository can be specified. This is used mostly for keeping dependency contexts correct.</param>
        /// <returns>True if package metadata was parsed successfully</returns>
        public static bool TryGetCommonHjsonMeta(
            this IScriptEngine engine,
            string text,
            string commentStartRegexPattern,
            string commentEndRegexPattern,
            ref PackageMeta packMeta,
            out VersionRequirement requiredVersion,
            CompletePackageReference packref = null,
            string parentRepo = "")
        {
            if (!engine.TryGetScriptMultilineMetaComment(
                text,
                commentStartRegexPattern,
                commentEndRegexPattern,
                out var metatext,
                out requiredVersion,
                packref))
                return false;

            packMeta = packMeta ?? new PackageMeta();
            packMeta.Text = text;
            packMeta.RequiredUppmVersion = requiredVersion;

            if (!engine.TryGetHjsonScriptMultilineMeta(
                metatext,
                ref packMeta,
                packref,
                parentRepo))
                return false;

            if (engine.TryGetScriptText(text, out var scripttext, packMeta.Imports, parentRepo))
            {
                packMeta.ScriptText = scripttext;
                return true;
            }
            else
            {
                Log.Error("Couldn't get the script text of {PackRef}.", packref?.ToString() ?? packMeta.Name);
                return false;
            }
        }
    }
}

