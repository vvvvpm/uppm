using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// <returns>True if metadata is found</returns>
        bool TryGetMeta(string text, ref PackageMeta packmeta, out VersionRequirement requiredVersion);

        /// <summary>
        /// Get the executable script text out of arbitrary raw package text.
        /// </summary>
        /// <param name="text">Raw contents of the package</param>
        /// <returns>The executable script text if found, or else null or empty string.</returns>
        string GetScriptText(string text);

        /// <summary>
        /// Run an action identified via a string from the script of a package
        /// </summary>
        /// <param name="pack">Input package</param>
        /// <param name="action">The specified action</param>
        void RunAction(Package pack, string action);
    }
}
