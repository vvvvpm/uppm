using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using uppm.Core.Scripting;

namespace uppm.Core.Repositories
{
    /// <summary>
    /// Represents a Git repository of packages, see readme.md for further documentation on how uppm expects folder structure.
    /// </summary>
    public class GitPackageRepository : IPackageRepository
    {
        /// <inheritdoc />
        public bool Ready { get; }

        /// <inheritdoc />
        public string Url { get; set; }

        /// <inheritdoc />
        public Exception RefreshError { get; }

        /// <inheritdoc />
        public bool TryGetPackage(PartialPackageReference reference, out Package package)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetScriptEngine(PartialPackageReference reference, out IScriptEngine extension)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetPackageText(PartialPackageReference reference, out string packtext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryInferPackageReference(PartialPackageReference incomplete, out CompletePackageReference complete)
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
        public ILogger Log { get; }

        public GitPackageRepository()
        {
            Log = this.GetContext();
        }
    }
}
