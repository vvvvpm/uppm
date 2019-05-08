using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace uppm.Core.Utils
{
    static public class GitUtils
    {
        private static void LogVerboseRepoOperationEvent(ILogging caller, RepositoryOperationContext context)
        {
            var logger = caller?.Log ?? Logging.L;
            logger.Debug("    submodule: {SubmoduleName}", context.SubmoduleName);
            logger.Debug("    recursion depth: {RepoRecDepth}", context.RecursionDepth);
            logger.Verbose("    parent: {ParentRepoName}", context.ParentRepositoryPath);
            logger.Verbose("    remote: {RepoRemoteName}", context.RemoteUrl);
        }

        private static RepositoryOperationStarting RepoOperationStart(ILogging caller, string action) => context =>
        {
            var logger = caller?.Log ?? Logging.L;
            logger.Information(
                "Repository {RepoAction} started on {RepoName}",
                action, 
                context.RepositoryPath);
            LogVerboseRepoOperationEvent(caller, context);
            return true;
        };

        private static RepositoryOperationCompleted RepoOperationEnd(ILogging caller, string action) => context =>
        {
            var logger = caller?.Log ?? Logging.L;
            logger.Information(
                "Repository {RepoAction} completed on {RepoName}",
                action,
                context.RepositoryPath);
            LogVerboseRepoOperationEvent(caller, context);
        };

        /// <summary>
        /// Clone a git repository. Caller must be provided.
        /// </summary>
        /// <param name="remote"></param>
        /// <param name="dst"></param>
        /// <param name="options"></param>
        /// <param name="caller">Required for log and progress origin</param>
        /// <returns></returns>
        /// <remarks>
        /// OnCheckoutProgress and OnTransferProgress will be overriden to invoke <see cref="Logging.OnAnyProgress"/>.
        /// OnProgress, RepositoryOperationStarting and RepositoryOperationCompleted will be overriden to log
        /// in the uppm Serilog system.
        /// </remarks>
        public static Repository Clone(string remote, string dst, CloneOptions options = null, ILogging caller = null)
        {
            options = options ?? new CloneOptions();
            var logger = caller?.Log ?? Logging.L;

            options.OnCheckoutProgress = (path, steps, totalSteps) =>
            {
                caller?.InvokeAnyProgress(totalSteps, steps, "Checking Out", path);
            };
            options.OnTransferProgress = progress =>
            {
                caller?.InvokeAnyProgress(progress.TotalObjects, progress.ReceivedObjects,"Transferring");
                return true;
            };
            options.OnProgress = output =>
            {
                logger.Debug(output);
                return true;
            };
            options.RepositoryOperationStarting = RepoOperationStart(caller, "Cloning");
            options.RepositoryOperationCompleted = RepoOperationEnd(caller, "Cloning");

            try
            {
                var resultPath = Repository.Clone(remote, dst, options);
                return new Repository(resultPath);
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Error during cloning repository {RepoRemoteName}", remote);
            }
            return null;
        }

        /// <summary>
        /// Synchronize an already existing repository folder.
        /// </summary>
        /// <param name="caller">Required for log and progress origin</param>
        /// <param name="repofolder"></param>
        /// <param name="fetchops"></param>
        /// <param name="checkoutops"></param>
        /// <returns></returns>
        /// <remarks>
        /// OnCheckoutProgress and OnTransferProgress will be overriden to invoke <see cref="Logging.OnAnyProgress"/>.
        /// OnProgress, RepositoryOperationStarting and RepositoryOperationCompleted will be overriden to log
        /// in the uppm Serilog system.
        /// </remarks>
        public static Repository Synchronize(string repofolder, FetchOptions fetchops = null, CheckoutOptions checkoutops = null, ILogging caller = null)
        {
            var logger = caller?.Log ?? Logging.L;
            try
            {
                var repo = new Repository(repofolder);
                fetchops = fetchops ?? new FetchOptions();
                checkoutops = checkoutops ?? new CheckoutOptions();

                fetchops.RepositoryOperationStarting = RepoOperationStart(caller, "Fetching");
                fetchops.RepositoryOperationCompleted = RepoOperationEnd(caller, "Fetching");
                fetchops.OnTransferProgress = progress =>
                {
                    caller?.InvokeAnyProgress(progress.TotalObjects, progress.ReceivedObjects, "Transferring");
                    return true;
                };
                fetchops.OnProgress = output =>
                {
                    logger.Debug(output);
                    return true;
                };
                checkoutops.OnCheckoutProgress = (path, steps, totalSteps) =>
                {
                    caller?.InvokeAnyProgress(totalSteps, steps, "Checking Out", path);
                };

                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, fetchops, "");
                Commands.Checkout(repo, "master", checkoutops);
                return repo;
            }
            catch (Exception e)
            {
                logger.Error(e, "Error opening or checking out locally available repository. ({RepoUrl})", repofolder);
                return null;
            }
        }
    }
}
