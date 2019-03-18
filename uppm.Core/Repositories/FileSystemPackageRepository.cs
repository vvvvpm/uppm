using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using uppm.Core.Scripting;

namespace uppm.Core.Repositories
{
    /// <summary>
    /// Represents a folder in the local filesystem (or a mounted network location)
    /// which acts like a repository of uppm packs.
    /// </summary>
    public class FileSystemPackageRepository : IPackageRepository
    {
        /// <inheritdoc />
        public bool Ready { get; }

        /// <inheritdoc />
        public string Url { get; set; }

        /// <inheritdoc />
        public Exception RefreshError { get; }

        private readonly Dictionary<CompletePackageReference, string> _packages = new Dictionary<CompletePackageReference, string>();

        /// <inheritdoc />
        public bool TryGetPackage(PartialPackageReference reference, out Package package) =>
            this.CommonTryGetPackage(reference, out package);

        /// <inheritdoc />
        public bool TryGetScriptEngine(PartialPackageReference reference, out IScriptEngine extension)
        {
            extension = null;
            if (!TryInferPackageReference(reference, out var cref)) return false;
            if (_packages.TryGetValue(cref, out var file))
            {
                var ext = Path.GetExtension(file)?.Trim('.') ?? "";
                return ScriptEngine.TryGetEngine(ext, out extension);
            }
            return false;
        }

        /// <inheritdoc />
        public bool TryGetPackageText(PartialPackageReference reference, out string packtext)
        {
            packtext = "";
            if (!TryInferPackageReference(reference, out var cref)) return false;

            if (!_packages.TryGetValue(cref, out var filename)) return false;
            Log.Verbose("Reading text of {$PackName}", cref);
            try
            {
                packtext = File.ReadAllText(filename);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Error reading text of {$PackName}", cref);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryInferPackageReference(PartialPackageReference incomplete, out CompletePackageReference complete)
        {
            return this.TryInferPackageReferenceFromCollection(_packages.Keys, incomplete, out complete);
        }

        /// <inheritdoc />
        public bool RepositoryExists() => RepositoryReferenceValid();

        /// <inheritdoc />
        public bool Refresh()
        {
            if (!RepositoryReferenceValid()) return false;
            Log.Debug("Scanning folder for packs: {RepoUrl}", Url);
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

                        foreach (var versionfile in Directory.EnumerateFiles(packdir))
                        {
                            var ext = Path.GetExtension(versionfile)?.Trim('.') ?? "";
                            if (!ScriptEngine.TryGetEngine(ext, out _)) continue;

                            var version = Path.GetFileNameWithoutExtension(versionfile);
                            _packages.Add(new CompletePackageReference
                            {
                                Name = pack,
                                RepositoryUrl = Url,
                                Version = version
                            }, versionfile);
                        }
                    }
                }
                Log.Debug("Found {PackCount} packs", _packages.Count);
                OnRefreshed?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Error occured during refreshing folder pack repository {RepoUrl}", Url);
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
        public ILogger Log { get; }

        public FileSystemPackageRepository()
        {
            Log = this.GetContext();
        }
    }
}
