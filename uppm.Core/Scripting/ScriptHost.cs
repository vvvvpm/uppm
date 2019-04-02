using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using md.stdl.Windows;
using Serilog;

namespace uppm.Core.Scripting
{
    /// <summary>
    /// A host or global object for scripts. Members can be referenced implicitly in scripts without the need for `ScriptHost.*`
    /// </summary>
    public class ScriptHost : ILogSource
    {
        /// <summary>
        /// Delegate for events fired by a script
        /// </summary>
        /// <param name="host"></param>
        public delegate void ScriptEventHandler(ScriptHost host);

        /// <summary>
        /// Delegate for events regarding a file or folder fired by a script
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="host"></param>
        public delegate void FileEventHandler(ScriptHost host, string path);

        /// <summary>
        /// Delegate for events regarding a file or folder source-destination pair fired by a script
        /// </summary>
        /// <param name="host"></param>
        /// <param name="srcpath">Source file/folder</param>
        /// <param name="dstpath">Destination file/folder</param>
        public delegate void FileDestinationEventHandler(ScriptHost host, string srcpath, string dstpath);

        /// <summary>
        /// Delegate for <see cref="OnCopyProgress"/>
        /// </summary>
        /// <param name="host"></param>
        /// <param name="fsentry">Currently processed <see cref="FileInfo"/> or <see cref="DirectoryInfo"/></param>
        /// <param name="srcdir">source directory</param>
        /// <param name="dstdir">destination directory</param>
        public delegate void CopyProgressHandler(ScriptHost host, FileSystemInfo fsentry, string srcdir, string dstdir);

        /// <summary>
        /// Event when a copying operation started
        /// </summary>
        public static event FileDestinationEventHandler OnCopyStarted;

        /// <summary>
        /// Event when a copying operation ended
        /// </summary>
        public static event FileDestinationEventHandler OnCopyEnded;

        /// <summary>
        /// Event occuring during a copy operation progress
        /// </summary>
        public static event CopyProgressHandler OnCopyProgress;

        /// <summary>
        /// Event when a deketing operation started
        /// </summary>
        public static event FileEventHandler OnDeleteStarted;

        /// <summary>
        /// Event when a deleting operation ended
        /// </summary>
        public static event FileEventHandler OnDeleteEnded;

        /// <summary>
        /// Event occuring during a delete operation progress
        /// </summary>
        public static event FileEventHandler OnDeleteProgress;

        /// <summary>
        /// Information about the target application of the package manager
        /// </summary>
        public TargetApplication Application { get; set; }

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
            OnCopyStarted?.Invoke(this, srcdir, dstdir);

            FileSystem.CopyDirectory(srcdir, dstdir, ignore, match, fsentry =>
            {
                OnCopyProgress?.Invoke(this, fsentry, srcdir, dstdir);
            });

            OnCopyEnded?.Invoke(this, srcdir, dstdir);
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
            OnDeleteStarted?.Invoke(this, srcdir);
            DeleteDirectoryRec(srcdir, recursive, ignore, match);
            OnDeleteEnded?.Invoke(this, srcdir);
        }

        private void DeleteDirectoryRec(string srcdir, bool recursive = true, string[] ignore = null, string[] match = null)
        {
            if (recursive)
            {
                var subfolders = Directory.GetDirectories(srcdir);
                foreach (var s in subfolders)
                {
                    var name = Path.GetFileName(s);
                    if (ignore != null && ignore.Any(path => new WildcardPattern(path).IsMatch(name))) continue;
                    if (match != null && !match.Any(path => new WildcardPattern(path).IsMatch(name))) continue;

                    OnDeleteProgress?.Invoke(this, s);
                    DeleteDirectoryRec(s, true, ignore, match);
                }
            }

            var files = Directory.GetFiles(srcdir);
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (ignore != null && ignore.Any(path => new WildcardPattern(path).IsMatch(name))) continue;
                if (match != null && !match.Any(path => new WildcardPattern(path).IsMatch(name))) continue;

                OnDeleteProgress?.Invoke(this, f);
                var attr = File.GetAttributes(f);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(f, attr ^ FileAttributes.ReadOnly);
                }
                File.Delete(f);
            }
            Directory.Delete(srcdir);
        }
    }
}
