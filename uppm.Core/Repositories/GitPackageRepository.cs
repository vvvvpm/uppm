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
    public class GitPackageRepository : IPackageRepository
    {
        private readonly Dictionary<CompletePackageReference, string> _packages = new Dictionary<CompletePackageReference, string>();

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
        
        /// <inheritdoc />
        public bool Refresh()
        {
            if (!RepositoryReferenceValid()) return false;
            var repofolder = Url.MatchGroup(@"^https?\:\/\/(?<reponame>.*?).git[\?\:\$]")["reponame"]?
                .Value?
                .Replace('/', '_');

            if (string.IsNullOrEmpty(repofolder)) return false;
            if (Repository.IsValid(repofolder))
            {
                GitRepository = GitUtils.Synchronize(this, repofolder, new FetchOptions
                {
                    CertificateCheck = CertificateCheck,
                    CredentialsProvider = CredentialsProvider,
                    CustomHeaders = CustomHeaders
                });
            }
            else
            {
                GitRepository = GitUtils.Clone(this, Url,
                    Path.Combine(Uppm.Implementation.TemporaryFolder, repofolder),
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
                    });
            }

            if (GitRepository != null)
            {
                Synchronized = true;
                var res = this.GatherPackageReferencesFromFolder(repofolder, _packages);
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
