using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.String;

namespace uppm.Core.Utils
{
    /// <summary>
    /// Collection of static utilities operating on entities of package repositories
    /// </summary>
    public static class RepositoryUtils
    {
        /// <summary>
        /// The default infered list of repositories if none is specified.
        /// </summary>
        /// <remarks>
        /// Entries must be specified by the implementer of uppm.Core
        /// </remarks>
        public static List<IPackageRepository> DefaultRepositories { get; } = new List<IPackageRepository>();

        /// <summary>
        /// Tries to convert a <see cref="PackageReference"/> into a valid one in the context of an
        /// <see cref="IPackageRepository"/> and a provided collection of available packages.
        /// </summary>
        /// <param name="repository">Target repository</param>
        /// <param name="packages">Collection of all available packages supplied by the repository</param>
        /// <param name="incomplete">The source <see cref="PackageReference"/></param>
        /// <param name="complete">The output <see cref="PackageReference"/> when found or null when there's no match</param>
        /// <returns>Whether infering found a valid package or not</returns>
        public static bool TryInferPackageReferenceFromCollection(
            this IPackageRepository repository,
            IEnumerable<PackageReference> packages,
            PackageReference incomplete,
            out PackageReference complete)
        {
            complete = null;
            if (incomplete?.Name == null) return false;

            // should the pack be in this repository?
            if (!string.IsNullOrWhiteSpace(incomplete.RepositoryUrl) &&
                !incomplete.RepositoryUrl.EqualsCaseless(repository.Url)) return false;

            // is the pack present in this repository?
            var candidates = packages.Where(p => p.Name.EqualsCaseless(incomplete.Name)).ToList();
            if (candidates.Count == 0) return false;

            // if special version just check for string equality
            if (incomplete.IsSpecialVersion)
            {
                var candidate = candidates.FirstOrDefault(p => p.PackVersion.EqualsCaseless(incomplete.PackVersion));
                if (candidate != null)
                {
                    complete = candidate;
                    return true;
                }
            }

            // if no version specified or explicitly "latest" is speciofied then get the latest
            var getlatest = incomplete.PackVersion == null || incomplete.IsLatest;
            if (getlatest)
            {
                // is a pack explicitly labeled "latest" exists already?
                var candidate = candidates.FirstOrDefault(p => p.IsLatest);
                if (candidate != null)
                {
                    complete = candidate;
                    return true;
                }

                // if not try to get the one with the highest semantical version
                var version = new UppmVersion(0, 0, 0, 0);

                foreach (var cand in candidates)
                {
                    if (cand.IsSemanticalVersion(out var semversion) && version < semversion)
                    {
                        version = semversion;
                        candidate = cand;
                    }
                }

                if (candidate != null)
                {
                    complete = candidate;
                    return true;
                }

                // pack has only special versions or no versions at all
                return false;
            }

            // if an optionally partial semantic version is explicitly specified
            if (incomplete.IsSemanticalVersion(out var insemver))
            {
                // get the latest semantic version inside the specified version scope
                var candidate = candidates.Where(p =>
                {
                    var valid = p.IsSemanticalVersion(out var psemver);
                    if (!valid) return false;
                    for (int i = 0; i < (int)insemver.Scope + 1; i++)
                    {
                        valid = psemver.Components[i] == insemver.Components[i];
                    }

                    return valid;
                })
                    .OrderByDescending(p =>
                        p.IsSemanticalVersion(out var psemver)
                            ? psemver
                            : new UppmVersion(0, 0, 0, 0))
                    .FirstOrDefault();
                if (candidate != null)
                {
                    complete = candidate;
                    return true;
                }
            }
            return false;
        }

        public static IPackageRepository CreateRepositoryCandidate(string repourl)
        {

        }
    }
}
