using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using uppm.Core.Utils;

namespace uppm.Core.Scripting
{
    /// <inheritdoc />
    /// <summary>
    /// When implemented represents a scripting environment. A script engine is mostly
    /// specific to a single language. Implementation has to provide exectution of
    /// different actions and ways to extract the package metadata and the actual
    /// executable script text from a raw package text.
    /// </summary>
    public interface IScriptEngine : ILogSource
    {
        /// <summary>
        /// Information about the currently running implementation of uppm
        /// </summary>
        IUppmImplementation Uppm { get; set; }

        /// <summary>
        /// File extension to be associated with this scriptengine, without starting dot.
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
        /// <param name="packref">Optional package reference for the desired pack to get the meta for</param>
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
        /// <param name="imports">Optional HashSet of imports found in this script</param>
        /// <returns>The executable script text if found, or else null or empty string.</returns>
        bool TryGetScriptText(string text, out string scripttext, HashSet<PartialPackageReference> imports);

        /// <summary>
        /// Run an action identified via a string from the script of a package
        /// </summary>
        /// <param name="pack">Input package</param>
        /// <param name="action">The specified action</param>
        void RunAction(Package pack, string action);
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

        public static void LoadEngines(Assembly assembly)
        {
            var enginetypes = assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IScriptEngine)));
            foreach (var enginetype in enginetypes)
            {
                var engine = enginetype.CreateInstance() as IScriptEngine;
                //if(engine == null) continue;
            }
        }
    }
}

