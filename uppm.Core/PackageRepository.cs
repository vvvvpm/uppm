using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using log4net;
using md.stdl.String;
using uppm.Core.Utils;

namespace uppm.Core
{
    /// <summary>
    /// Represents a repository of packages
    /// </summary>
    public interface IPackageRepository
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
        bool TryGetPackage(PackageReference reference, out Package package);

        /// <summary>
        /// Tries to get the text of a package based on a reference without parsing it into a Package object
        /// </summary>
        /// <param name="reference">Reference of the desired package</param>
        /// <param name="packtext">The text of the package if found, otherwise null or empty-string</param>
        /// <returns>True if package is found</returns>
        bool TryGetPackageText(PackageReference reference, out string packtext);

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
        bool TryInferPackageReference(PackageReference incomplete, out PackageReference complete);

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
        bool Refresh();

        /// <summary>
        /// Event fired when refreshing the Repository contents have been finished.
        /// </summary>
        event EventHandler OnRefreshed;
    }

    /// <summary>
    /// Represents a Git repository of packages, see readme.md for further documentation on how uppm expects folder structure.
    /// </summary>
    public class GitPackageRepository : IPackageRepository, ILogSource
    {
        /// <inheritdoc />
        public bool Ready { get; }

        /// <inheritdoc />
        public string Url { get; set; }

        /// <inheritdoc />
        public Exception RefreshError { get; }
        
        /// <inheritdoc />
        public bool TryGetPackage(PackageReference reference, out Package package)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetPackageText(PackageReference reference, out string packtext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryInferPackageReference(PackageReference incomplete, out PackageReference complete)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool RepositoryExists()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Refresh()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public event EventHandler OnRefreshed;

        /// <inheritdoc />
        public bool RepositoryReferenceValid() => Regex.IsMatch(Url, @"^https?\:\/\/.*?\.git[\?\:\$]");

        /// <inheritdoc />
        public ILog Log { get; set; }
    }

    public class FileSystemPackageRepository : IPackageRepository, ILogSource
    {
        /// <inheritdoc />
        public bool Ready { get; }

        /// <inheritdoc />
        public string Url { get; set; }

        /// <inheritdoc />
        public Exception RefreshError { get; }

        private readonly Dictionary<PackageReference, string> _packages = new Dictionary<PackageReference, string>();

        /// <inheritdoc />
        public bool TryGetPackage(PackageReference reference, out Package package)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetPackageText(PackageReference reference, out string packtext)
        {
            packtext = "";
            if (!TryInferPackageReference(reference, out var cref)) return false;

            if (!_packages.TryGetValue(cref, out var filename)) return false;
            Log.UDebug($"Reading text of {cref.Name}", this);
            try
            {
                packtext = File.ReadAllText(filename);
                return true;
            }
            catch (Exception e)
            {
                Log.UError($"Error reading text of {cref.Name}", this, e);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryInferPackageReference(PackageReference incomplete, out PackageReference complete)
        {
            return this.TryInferPackageReferenceFromCollection(_packages.Keys, incomplete, out complete);
        }

        /// <inheritdoc />
        public bool RepositoryExists() => RepositoryReferenceValid();

        /// <inheritdoc />
        public bool Refresh()
        {
            if (!RepositoryReferenceValid()) return false;
            Log?.UInfo($"Scanning folder for packs: {Url.Replace('/', '\\')}", this);
            _packages.Clear();

            try
            {
                foreach (var authordir in Directory.EnumerateDirectories(Url.Replace('/', '\\')))
                {
                    var author = Path.GetFileName(authordir);
                    if (string.IsNullOrEmpty(author)) author = Path.GetDirectoryName(authordir);

                    foreach (var packdir in Directory.EnumerateDirectories(authordir))
                    {
                        var pack = Path.GetFileName(packdir);
                        if (string.IsNullOrEmpty(pack)) pack = Path.GetDirectoryName(packdir);

                        foreach (var versionfile in Directory.EnumerateFiles(packdir, "*.up"))
                        {
                            var version = Path.GetFileNameWithoutExtension(versionfile);
                            _packages.Add(new PackageReference
                            {
                                Name = pack,
                                RepositoryUrl = Url,
                                PackVersion = version
                            }, versionfile);
                        }
                    }
                }
                Log?.UInfo($"Found {_packages.Count} packs", this);
                OnRefreshed?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception e)
            {
                Log?.UError($"Error occured during refreshing folder pack repository:\n{Url}", this, e);
                return false;
            }

        }

        /// <inheritdoc />
        public event EventHandler OnRefreshed;

        /// <inheritdoc />
        public bool RepositoryReferenceValid()
        {
            if (Url.StartsWith("\\\\") ||
                Url.StartsWith("//") ||
                Regex.IsMatch(Url, @"^\w\:[\\\/]"))
            {
                return Directory.Exists(Url.Replace('/', '\\'));
            }
            return false;
        }

        /// <inheritdoc />
        public ILog Log { get; set; }
    }
}

