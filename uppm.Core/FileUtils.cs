using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Windows;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace uppm.Core
{
    //TODO: put most of this in Unimpressive.Core

    /// <summary>
    /// Utilities regarding file operations
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Copy a directory recursively with either black~ or white listing.
        /// </summary>
        /// <param name="srcdir">Source directory</param>
        /// <param name="dstdir">Destination directory</param>
        /// <param name="ignore">Ignoring blacklist, can use wildcards</param>
        /// <param name="match">Matching whitelist, can use wildcards</param>
        /// <param name="caller">optional caller </param>
        public static void CopyDirectory(
            string srcdir, string dstdir,
            string[] ignore = null, string[] match = null,
            ILogging caller = null)
        {
            var logger = caller?.Log ?? Logging.L;
            logger.Information(
                "Copy started from {Src} to {Dst}",
                srcdir,
                dstdir
            );

            FileSystem.CopyDirectory(srcdir, dstdir, ignore, match, fsentry =>
            {
                caller.InvokeAnyProgress(
                    message: "Copying " + Path.GetDirectoryName(fsentry.FullName),
                    state: fsentry.Name
                );
            });

            logger.Debug(
                "Copy ended from {Src} to {Dst}",
                srcdir,
                dstdir
            );
        }

        /// <summary>
        /// Delete a directory recursively with either black~ or white listing.
        /// </summary>
        /// <param name="srcdir">Source directory</param>
        /// <param name="recursive"></param>
        /// <param name="ignore">Ignoring blacklist, can use wildcards</param>
        /// <param name="match">Matching whitelist, can use wildcards</param>
        public static void DeleteDirectory(
            string srcdir,
            bool recursive = true,
            string[] ignore = null, string[] match = null,
            ILogging caller = null)
        {
            var logger = caller?.Log ?? Logging.L;
            logger.Information(
                "Started deleting directory {Src}",
                srcdir
            );

            DeleteDirectoryRec(srcdir, recursive, ignore, match);

            logger.Debug(
                "Deleted {Src}",
                srcdir
            );
        }

        private static void DeleteDirectoryRec(
            string srcdir,
            bool recursive = true,
            string[] ignore = null, string[] match = null,
            ILogging caller = null)
        {
            var logger = caller?.Log ?? Logging.L;
            if (recursive)
            {
                var subfolders = Directory.GetDirectories(srcdir);
                foreach (var s in subfolders)
                {
                    var name = Path.GetFileName(s);
                    if (ignore != null && ignore.Any(path => new WildcardPattern(path).IsMatch(name))) continue;
                    if (match != null && !match.Any(path => new WildcardPattern(path).IsMatch(name))) continue;

                    caller.InvokeAnyProgress(
                        message: "Deleting " + Path.GetDirectoryName(s),
                        state: Path.GetFileName(s)
                    );

                    DeleteDirectoryRec(s, true, ignore, match);
                }
            }

            var files = Directory.GetFiles(srcdir);
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (ignore != null && ignore.Any(path => new WildcardPattern(path).IsMatch(name))) continue;
                if (match != null && !match.Any(path => new WildcardPattern(path).IsMatch(name))) continue;
                
                caller.InvokeAnyProgress(
                    message: "Deleting " + srcdir,
                    state: name
                );

                try
                {
                    var attr = File.GetAttributes(f);
                    if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(f, attr ^ FileAttributes.ReadOnly);
                    }
                    File.Delete(f);
                }
                catch (Exception e)
                {
                    logger.Error(
                        e,
                        "Error during deleting {Src}",
                        f
                    );
                }
            }

            try
            {
                Directory.Delete(srcdir);
            }
            catch (Exception e)
            {
                logger.Error(
                    e,
                    "Error during deleting {Src}",
                    srcdir
                );
            }
        }

        /// <summary>
        /// Extract an archive which is supported by the SharpCompress library (i.e.: zip, 7z, rar, tar.bz2, etc...)
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dstdir"></param>
        /// <param name="caller"></param>
        public static void ExtractArchive(string src, string dstdir, ILogging caller = null)
        {
            var logger = caller?.Log ?? Logging.L;
            try
            {
                var archive = ArchiveFactory.Open(src, new ReaderOptions
                {
                    LookForHeader = true
                });

                var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
                var ii = 0;

                foreach (var entry in entries)
                {
                    caller?.InvokeAnyProgress(entries.Count, ii, entry.Key, $"Extracting");
                    entry.WriteToDirectory(dstdir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                    ii++;
                }
                archive.Dispose();
            }
            catch (Exception e)
            {
                logger.Error(e, "Error during extracting {Archive}", src);
            }
        }
    }
}
