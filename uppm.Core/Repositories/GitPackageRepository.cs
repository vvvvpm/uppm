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
        /// <inheritdoc />
        public bool Ready { get; }

        /// <inheritdoc />
        public string Url { get; set; }

        /// <inheritdoc />
        public Exception RefreshError { get; }

        public Func<string, IFlurlRequest> RequestConfiguration { get; set; }

        public Repository GitRepository { get; private set; }

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
            if (!RepositoryReferenceValid()) return false;
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
                    return false;
                }
                Log.Verbose("{RepoUrl} does exist", Url);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(
                    e,
                    "{RepoUrl} doesn't exist. Exception occured.",
                    Url);
                return false;
            }
        }

        public CertificateCheckHandler CertificateCheck { get; set; } = (certificate, valid, host) => true;
        public CredentialsHandler CredentialsProvider { get; set; } = (url, fromUrl, types) => new DefaultCredentials();

        /// <inheritdoc />
        public bool Refresh()
        {
            if (!RepositoryReferenceValid()) return false;
            var repofolder = Url.MatchGroup(@"^https?\:\/\/(?<reponame>.*?).git[\?\:\$]")["reponame"]
                .Value
                .Replace('/', '_');

            //TODO: if repo exist open and checkout (cloning ain't gonna work of course)

            GitRepository = GitUtils.Clone(this, Url,
                Path.Combine(Uppm.Implementation.TemporaryFolder, repofolder),
                new CloneOptions
                {
                    CertificateCheck = CertificateCheck,
                    CredentialsProvider = CredentialsProvider,
                });
            return GitRepository != null;
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
