using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using LibGit2Sharp;
using md.stdl.Windows;
using Serilog;
using uppm.Core.Utils;

namespace uppm.Core.Scripting
{
    /// <summary>
    /// A host or global object for scripts. Members can be referenced implicitly in scripts without the need for `ScriptHost.*`
    /// </summary>
    public class ScriptHost : ILogging
    {
        /// <summary>
        /// Information about the target application of the package manager
        /// </summary>
        public TargetApp App { get; set; }

        /// <summary>
        /// Information about the package manager
        /// </summary>
        public IUppmImplementation Uppm { get; }

        /// <summary>
        /// The package which script is currently hosted
        /// </summary>
        public Package Pack { get; }
        
        /// <inheritdoc />
        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);

        /// <summary></summary>
        /// <param name="pack">The associated package</param>
        /// <param name="uppm">The executing package manager</param>
        public ScriptHost(Package pack, IUppmImplementation uppm) : this()
        {
            Uppm = uppm;
            Pack = pack;
        }

        internal ScriptHost()
        {
            Log = this.GetContext();
        }

        /// <summary>
        /// Just a convenience shortener for <see cref="Action"/>`s, so the pack developer can spare `new `
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public Action Action(Action action) => action;
        
        /// <summary>
        /// Gracefully throw exception and tell about it in the package manager
        /// </summary>
        /// <param name="e"></param>
        public void ThrowException(Exception e)
        {
            Log.Error(e, "Script of {$PackRef} threw an exception", Pack.Meta.Self);
        }

        /// <summary>
        /// Copy a directory recursively with either black~ or white listing.
        /// </summary>
        /// <param name="srcdir">Source directory</param>
        /// <param name="dstdir">Destination directory</param>
        /// <param name="ignore">Ignoring blacklist, can use wildcards</param>
        /// <param name="match">Matching whitelist, can use wildcards</param>
        public void CopyDirectory(string srcdir, string dstdir, string[] ignore = null, string[] match = null)
        {
            FileUtils.CopyDirectory(srcdir, dstdir, ignore, match, this);
        }

        /// <summary>
        /// Clones a git repository. Only HTTP remote supported
        /// </summary>
        /// <param name="remote"></param>
        /// <param name="dstdir"></param>
        /// <param name="options"></param>
        public void GitClone(string remote, string dstdir, string branch = null, CloneOptions options = null)
        {
            options = options ?? new CloneOptions();
            options.BranchName = branch ?? options.BranchName;

            GitUtils.Clone(remote, dstdir, options, this);
        }

        /// <summary>
        /// Delete a directory recursively with either black~ or white listing.
        /// </summary>
        /// <param name="srcdir">Source directory</param>
        /// <param name="recursive"></param>
        /// <param name="ignore">Ignoring blacklist, can use wildcards</param>
        /// <param name="match">Matching whitelist, can use wildcards</param>
        public void DeleteDirectory(string srcdir, bool recursive = true, string[] ignore = null, string[] match = null)
        {
            FileUtils.DeleteDirectory(srcdir, recursive, ignore, match, this);
        }
    
    }
}
