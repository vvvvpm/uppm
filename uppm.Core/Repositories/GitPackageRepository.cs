using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flurl.Http;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using md.stdl.String;
using Serilog;
using uppm.Core.Scripting;
using uppm.Core.Utils;

namespace uppm.Core.Repositories
{
    /// <summary>
    /// Represents a Git repository of packages, see readme.md for further documentation on how uppm expects folder structure.
    /// </summary>
    /// <remarks>
    /// So far git repositories are only supported via https. (Github or Gitlab style
    /// </remarks>
    public class GitPackageRepository : IPackageRepository
    {
        /// <summary>
        /// Actively synchronize git repositories (fetch/pull) even if it has been already synchronized
        /// over the lifetime of this appdomain
        /// </summary>
        public static bool ForceSynchronize { get; set; } = false;

        private readonly Dictionary<CompletePackageReference, string> _packages = new Dictionary<CompletePackageReference, string>();
        private string _localRepositoryFolder;

        /// <inheritdoc />
        public bool Ready { get; private set; }

        /// <inheritdoc />
        public string Url { get; set; }

        /// <inheritdoc />
        public Exception RefreshError { get; }

        /// <summary>
        /// Configure HTTP request used for existance checking. Input is URL string, Output is <see cref="IFlurlRequest"/>
        /// </summary>
        public Func<string, IFlurlRequest> RequestConfiguration { get; set; }

        /// <summary>
        /// SSL certificate check of server. Default is naive and insecure.
        /// </summary>
        public CertificateCheckHandler CertificateCheck { get; set; } = (certificate, valid, host) => true;

        /// <summary>
        /// If remote repository server needs credentials for authentication, you can provide it here
        /// </summary>
        public CredentialsHandler CredentialsProvider { get; set; } = (url, fromUrl, types) => new DefaultCredentials();

        /// <summary>
        /// If remote repository server requires custom headers, they can be provided here
        /// </summary>
        public string[] CustomHeaders { get; set; }

        /// <summary>
        /// Underlying git repository
        /// </summary>
        public Repository GitRepository { get; private set; }

        /// <summary>
        /// This repository was checked to be existing over the lifetime of this appdomain
        /// </summary>
        public bool Exists { get; private set; }

        /// <summary>
        /// This repository was successfully synchronized at least once over the lifetime of this appdomain
        /// </summary>
        public bool Synchronized { get; private set; }

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
        public bool RepositoryExists()
        {
            if (!RepositoryReferenceValid()) return Exists = false;
            if (Exists) return true;
            Log.Information("Checking if {RepoUrl} exists?", Url);
            try
            {
                var response = RequestConfiguration == null
                    ? Url.GetAsync(completionOption: HttpCompletionOption.ResponseHeadersRead)
                    : RequestConfiguration(Url).GetAsync(completionOption: HttpCompletionOption.ResponseHeadersRead);
                response.Wait();
                if (response.Result.StatusCode != HttpStatusCode.OK)
                {
                    Log.Error(
                        "{RepoUrl} doesn't exist. Server responded with {StatusCodeInt} : {$StatusCode}",
                        Url,
                        (int)response.Status,
                        response.Status);
                    return Exists = false;
                }
                Log.Verbose("{RepoUrl} does exist", Url);
                return Exists = true;
            }
            catch (Exception e)
            {
                Log.Error(
                    e,
                    "{RepoUrl} doesn't exist. Exception occured.",
                    Url);
                return Exists = false;
            }
        }

        /// <summary>
        /// The local folder where the repository is stored
        /// </summary>
        public string LocalRepositoryFolder
        {
            get
            {
                if(string.IsNullOrEmpty(_localRepositoryFolder))
                {
                    _localRepositoryFolder = Url.MatchGroup(@"^https?\:\/\/(?<reponame>.*?).git[\?\:\$]")["reponame"]?
                        .Value?
                        .Replace('/', '_');
                    _localRepositoryFolder = Path.Combine(Uppm.Implementation.TemporaryFolder, _localRepositoryFolder);
                }
                return _localRepositoryFolder;
            }
        }
        
        /// <inheritdoc />
        public bool Refresh()
        {
            if (!RepositoryReferenceValid()) return false;

            if (string.IsNullOrEmpty(LocalRepositoryFolder)) return false;
            if (Repository.IsValid(LocalRepositoryFolder))
            {
                GitRepository = GitUtils.Synchronize(
                    LocalRepositoryFolder,
                    new FetchOptions
                    {
                        CertificateCheck = CertificateCheck,
                        CredentialsProvider = CredentialsProvider,
                        CustomHeaders = CustomHeaders
                    },
                    caller: this
                );
            }
            else
            {
                GitRepository = GitUtils.Clone(
                    Url,
                    LocalRepositoryFolder,
                    new CloneOptions
                    {
                        CertificateCheck = CertificateCheck,
                        CredentialsProvider = CredentialsProvider,
                        FetchOptions =
                        {
                            CertificateCheck = CertificateCheck,
                            CredentialsProvider = CredentialsProvider,
                            CustomHeaders = CustomHeaders
                        }
                    },
                    this
                );
            }

            if (GitRepository != null)
            {
                Synchronized = true;
                var res = this.GatherPackageReferencesFromFolder(LocalRepositoryFolder, _packages);
                OnRefreshed?.Invoke(this, EventArgs.Empty);
                Ready = true;
                return res;
            }
            return false;
        }

        /// <inheritdoc />
        public event EventHandler OnRefreshed;

        /// <inheritdoc />
        public bool RepositoryReferenceValid() => Regex.IsMatch(Url, @"^https?\:\/\/.*?\.git[\?\:\$]");

        /// <inheritdoc />
        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);
        
        public GitPackageRepository()
        {
            Log = this.GetContext();
        }
    }
}
