using System;
using uppm.Core.Scripting;

namespace uppm.Core.Repositories
{
    /// <summary>
    /// Represents a repository of packages
    /// </summary>
    public interface IPackageRepository : ILogging
    {
        /// <summary>
        /// Repository is loaded successfully and ready to be used.
        /// </summary>
        bool Ready { get; }

        /// <summary>
        /// Url pointing to the repository
        /// </summary>
        string Url { get; set; }

        /// <summary>
        /// Last exception during <see cref="Refresh"/>
        /// </summary>
        Exception RefreshError { get; }

        /// <summary>
        /// Tries to get a package based on a reference
        /// </summary>
        /// <param name="reference">Reference of the desired package</param>
        /// <param name="package">The output package if found, otherwise null</param>
        /// <returns>True if package is found</returns>
        bool TryGetPackage(PartialPackageReference reference, out Package package);

        /// <summary>
        /// Gets the proper script engine for a pack
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        bool TryGetScriptEngine(PartialPackageReference reference, out IScriptEngine extension);

        /// <summary>
        /// Tries to get the text of a package based on a reference without parsing it into a Package object
        /// </summary>
        /// <param name="reference">Reference of the desired package</param>
        /// <param name="packtext">The text of the package if found, otherwise null or empty-string</param>
        /// <returns>True if package is found</returns>
        bool TryGetPackageText(PartialPackageReference reference, out string packtext);

        /// <summary>
        /// Tries to infer the missing parts of a package reference and outputs a complete reference if found.
        /// </summary>
        /// <param name="incomplete">The partial reference with incomplete/missing data</param>
        /// <param name="complete">complete reference if found, otherwise null</param>
        /// <returns>True if inference was successful</returns>
        /// <remarks>
        /// This should be also called by <see cref="TryGetPackage"/> and <see cref="TryGetPackageText"/>
        /// before trying to resolve the input package reference
        /// </remarks>
        bool TryInferPackageReference(PartialPackageReference incomplete, out CompletePackageReference complete);

        /// <summary>
        /// Is the Reference to this repository syntactically valid and makes sense?
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This is different to <see cref="RepositoryExists"/> because it shouldn't do IO checks, only make sure syntax is valid.
        /// </remarks>
        bool RepositoryReferenceValid();

        /// <summary>
        /// Does the repository really exists?
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This is different to <see cref="RepositoryReferenceValid"/> because this function have to actively check
        /// of the repository, not just the syntax.
        /// </remarks>
        bool RepositoryExists();

        /// <summary>
        /// Refresh the available packages of a repository, or reinitialize one.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// uppm assumes this function is blocking current thread until refreshing is complete.
        /// If you have an inherently asynchronous operation in there, please wait inside the
        /// implementation method's body until that's complete.
        /// </remarks>
        bool Refresh();

        /// <summary>
        /// Event fired when refreshing the Repository contents have been finished.
        /// </summary>
        event EventHandler OnRefreshed;
    }
}

