using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using md.stdl.String;
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
        private readonly Dictionary<CompletePackageReference, string> _packages = new Dictionary<CompletePackageReference, string>();
        private string _absolutePath;

        /// <inheritdoc />
        public bool Ready { get; private set; }

        /// <inheritdoc />
        public string Url { get; set; }
        
        /// <inheritdoc />
        public Exception RefreshError { get; private set; }

        /// <summary>
        /// Absolute path to the desired directory. Derived from Url
        /// </summary>
        public string AbsolutePath
        {
            get
            {
                if (string.IsNullOrEmpty(_absolutePath)) RepositoryReferenceValid();
                return _absolutePath;
            }
        }

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
        public bool TryInferPackageReference(PartialPackageReference incomplete, out CompletePackageReference complete) =>
            this.TryInferPackageReferenceFromCollection(_packages.Keys, incomplete, out complete);

        /// <inheritdoc />
        public bool RepositoryExists() => RepositoryReferenceValid();

        /// <inheritdoc />
        public bool Refresh()
        {
            if (!RepositoryReferenceValid()) return false;
            var res = this.GatherPackageReferencesFromFolder(AbsolutePath, _packages);
            OnRefreshed?.Invoke(this, EventArgs.Empty);
            Ready = true;
            return res;
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
                _absolutePath = Url.Replace('/', '\\');
                return Directory.Exists(_absolutePath);
            }
            if (Url.StartsWithCaselessAny("\\", "/", ".\\", "./", "..\\", "../"))
            {
                _absolutePath = Path.GetFullPath(Path.Combine(Uppm.WorkingDirectory, Url.Replace('/', '\\')));
                return Directory.Exists(_absolutePath);
            }
            return false;
        }

        /// <inheritdoc />
        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);

        public FileSystemPackageRepository()
        {
            Log = this.GetContext();
        }
    }
}
