using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace uppm.Core.Utils
{
    static public class GitUtils
    {
        private static void LogVerboseRepoOperationEvent(ILogSource caller, RepositoryOperationContext context)
        {
            caller.Log.Debug("    submodule: {SubmoduleName}", context.SubmoduleName);
            caller.Log.Debug("    recursion depth: {RepoRecDepth}", context.RecursionDepth);
            caller.Log.Verbose("    parent: {ParentRepoName}", context.ParentRepositoryPath);
            caller.Log.Verbose("    remote: {RepoRemoteName}", context.RemoteUrl);
        }

        /// <summary>
        /// Clone a git repository. Caller must be provided.
        /// </summary>
        /// <param name="caller">Required for log and progress origin</param>
        /// <param name="remote"></param>
        /// <param name="dst"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>
        /// OnCheckoutProgress and OnTransferProgress will be overriden to invoke <see cref="UppmLog.OnAnyProgress"/>.
        /// OnProgress, RepositoryOperationStarting and RepositoryOperationCompleted will be overriden to log
        /// in the uppm Serilog system.
        /// </remarks>
        public static Repository Clone(ILogSource caller, string remote, string dst, CloneOptions options = null)
        {
            options = options ?? new CloneOptions();

            options.OnCheckoutProgress = (path, steps, totalSteps) =>
            {
                caller.InvokeAnyProgress(totalSteps, steps, "Checking Out", path);
            };
            options.OnTransferProgress = progress =>
            {
                caller.InvokeAnyProgress(progress.TotalObjects, progress.ReceivedObjects,"Transferring");
                return true;
            };
            options.OnProgress = output =>
            {
                caller.Log.Debug(output);
                return true;
            };
            options.RepositoryOperationStarting = context =>
            {
                caller.Log.Information("Repository operation started on {RepoName}", context.RepositoryPath);
                LogVerboseRepoOperationEvent(caller, context);
                return true;
            };
            options.RepositoryOperationCompleted = context =>
            {
                caller.Log.Information("Repository operation completed on {RepoName}", context.RepositoryPath);
                LogVerboseRepoOperationEvent(caller, context);
            };

            try
            {
                var resultPath = Repository.Clone(remote, dst, options);
                return new Repository(resultPath);
            }
            catch (Exception e)
            {
                caller.Log.Fatal(e, "Error during cloning repository {RepoRemoteName}", remote);
            }
            return null;
        }
    }
}
