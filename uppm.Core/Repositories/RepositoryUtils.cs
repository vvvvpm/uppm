using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fasterflect;
using md.stdl.Coding;
using md.stdl.String;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace uppm.Core.Repositories
{
    /// <summary>
    /// Collection of static utilities operating on entities of package repositories
    /// </summary>
    public static class PackageRepository
    {
        private static Dictionary<string, IPackageRepository> DefaultRepositories { get; } = new Dictionary<string, IPackageRepository>();

        /// <summary>
        /// Repositories which have been used at least once during the operation of uppm.
        /// </summary>
        private static Dictionary<string, IPackageRepository> PresentRepositories { get; } = new Dictionary<string, IPackageRepository>();

        /// <summary>
        /// Set of known repository types which can be used to instantiate new repositories.
        /// </summary>
        public static HashSet<Type> KnownRepositoryTypes { get; } = new HashSet<Type>();

        static PackageRepository()
        {
            LoadRepositoryTypes(typeof(PackageRepository).Assembly);
        }

        /// <summary>
        /// If implementer has their own repository types, they must call this method
        /// before any other operations take place.
        /// </summary>
        /// <param name="assembly"></param>
        public static void LoadRepositoryTypes(Assembly assembly)
        {
            var repotypes = assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPackageRepository)));
            foreach (var type in repotypes)
            {
                KnownRepositoryTypes.Update(type);
            }
        }

        /// <summary>
        /// Register this repository as a default one, which will be used to infer
        /// package references without reference to specific repository.
        /// </summary>
        /// <remarks>
        /// Default repositories must be specified and completely initialized by the
        /// implementer of uppm.Core before any other uppm operations take place.
        /// </remarks>
        public static void SetDefaultRepository(this IPackageRepository repository)
        {
            DefaultRepositories.UpdateGeneric(repository.Url, repository);
            PresentRepositories.UpdateGeneric(repository.Url, repository);
        }
        
        internal static void RegisterRepository(this IPackageRepository repository)
        {
            PresentRepositories.UpdateGeneric(repository.Url, repository);
        }

        /// <summary>
        /// Tries to get or create a <see cref="IPackageRepository"/> instance for a reference
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="repo">The output repository</param>
        /// <returns>True if getting a repository was successful</returns>
        public static bool TryGetRepository(this PartialPackageReference reference, out IPackageRepository repo)
        {
            repo = null;
            if (reference == null) return false;
            if (!string.IsNullOrWhiteSpace(reference.RepositoryUrl))
            {
                var res = DefaultRepositories.TryGetValue(reference.RepositoryUrl, out repo);
                if (!res) res = PresentRepositories.TryGetValue(reference.RepositoryUrl, out repo);
                if (!res && TryCreateRepository(reference.RepositoryUrl, out repo))
                {
                    repo.RegisterRepository();
                    res = repo.Refresh();
                }

                if (res) return true;
                else return false;
            }
            else
            {
                UppmLog.L.Verbose("Repository of {$PackRef} is not specified, looking for it in default repositories", reference);
                foreach (var defrepo in DefaultRepositories.Values)
                {
                    if (defrepo.TryGetPackageText(reference, out _))
                    {
                        repo = defrepo;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Tries to create a <see cref="Package"/> instance for a reference
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="pack">The output package</param>
        /// <returns>True if creating a package was successful</returns>
        public static bool TryGetPackage(this PartialPackageReference reference, out Package pack)
        {
            pack = null;
            if (reference == null) return false;
            if (!reference.TryGetRepository(out var repo))
            {
                UppmLog.L.Error("Couldn't get repository for {$PackRef}", reference);
                return false;
            }

            if (!repo.TryGetPackage(reference, out pack))
            {
                repo.Log.Error("Couldn't get package for {$PackRef} in repository {RepoUrl}", reference, repo.Url);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to create a previously unknown <see cref="IPackageRepository"/>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="repo">The output repository</param>
        /// <returns>True if creating a repository was successful</returns>
        public static bool TryCreateRepository(string url, out IPackageRepository repo)
        {
            repo = null;
            if (url == null) return false;

            foreach (var repotype in KnownRepositoryTypes)
            {
                var candidate = repotype.CreateInstance() as IPackageRepository;
                if(candidate == null) continue;
                candidate.Url = url;
                if (!candidate.RepositoryReferenceValid()) continue;
                if (!candidate.RepositoryExists()) continue;
                repo = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Common implementation of creating a package from specified repository.
        /// </summary>
        /// <param name="repo">Target repository</param>
        /// <param name="reference"></param>
        /// <param name="package">The output package</param>
        /// <returns>True if creating a package was successful</returns>
        public static bool CommonTryGetPackage(this IPackageRepository repo, PartialPackageReference reference, out Package package)
        {
            package = null;
            PackageMeta packmeta = null;

            if (!repo.TryInferPackageReference(reference, out var cref))
            {
                repo.Log.Error("{$PackRef} cannot be found in repository {RepoUrl}", reference, repo.Url);
                return false;
            }
            repo.Log.Verbose("Complete reference of {$PartialPackRef} is {$PackRef}", reference, cref);

            if (!repo.TryGetScriptEngine(cref, out var engine))
            {
                repo.Log.Error("Cannot infer script engine for {$PackRef} in repository {RepoUrl}", reference, repo.Url);
                return false;
            }
            repo.Log.Verbose("Infered script engine of {$PackRef} is {EngineExtension}", cref, engine.Extension);

            if (!repo.TryGetPackageText(cref, out var packtext))
            {
                repo.Log.Error("Couldn't get text for {$PackRef} in repository {RepoUrl}", reference, repo.Url);
                return false;
            }

            if (!engine.TryGetMeta(packtext, ref packmeta, out var requiredVersion, cref))
            {
                repo.Log.Error("Couldn't get script text for {$PackRef} or it requires a newer version of uppm. (at engine `{EngineExtension}`)", reference, engine.Extension);
                return false;
            }

            packmeta.Version = cref.Version;

            package = new Package(packmeta, engine);
            return true;
        }

        /// <summary>
        /// Tries to convert a <see cref="PartialPackageReference"/> into a <see cref="CompletePackageReference"/>
        /// one in the context of an <see cref="IPackageRepository"/> and a provided collection of available packages.
        /// </summary>
        /// <param name="repository">Target repository</param>
        /// <param name="packages">Collection of all available packages supplied by the repository</param>
        /// <param name="incomplete">The source <see cref="PartialPackageReference"/></param>
        /// <param name="complete">The output <see cref="CompletePackageReference"/> when found or null when there's no match</param>
        /// <returns>Whether infering found a valid package or not</returns>
        public static bool TryInferPackageReferenceFromCollection(
            this IPackageRepository repository,
            IEnumerable<CompletePackageReference> packages,
            PartialPackageReference incomplete,
            out CompletePackageReference complete)
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
                var candidate = candidates.FirstOrDefault(p => p.Version.EqualsCaseless(incomplete.Version));
                if (candidate != null)
                {
                    complete = candidate;
                    return true;
                }
            }

            // if no version specified or explicitly "latest" is speciofied then get the latest
            var getlatest = incomplete.Version == null || incomplete.IsLatest;
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
    }
}
