using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hjson;
using md.stdl.Coding;
using md.stdl.String;
using Newtonsoft.Json.Linq;
using Serilog;
using uppm.Core.Repositories;
using uppm.Core.Scripting;

namespace uppm.Core
{
    /// <summary>
    /// Actual package containing side effects and practical utilities
    /// </summary>
    /// <remarks>
    /// When <see cref="ConstructDependencyTree()"/> is called
    /// dependencies are flattened to the initial dependency referenced
    /// by user input or other manual means. This avoids duplicates,
    /// circular dependencies and version conflicts but sacrifices
    /// nice graph-visualizations (so far ;) )
    /// </remarks>
    public class Package : ILogSource
    {
        private Package _root;

        /// <summary>
        /// Metadata of the package
        /// </summary>
        public PackageMeta Meta { get; }

        /// <summary>
        /// Associated script engine with the package in runtime
        /// </summary>
        public IScriptEngine Engine { get; }

        /// <summary>
        /// Branch depth in the dependency tree counted from root == 0
        /// </summary>
        public int DependencyTreeDepth { get; set; }

        /// <summary>
        /// The package at the root of the dependency tree.
        /// </summary>
        public Package Root
        {
            get => _root ?? this;
            set => _root = value;
        }

        /// <summary>
        /// If this package is root all recursive dependencies can be
        /// collected into this dictionary.
        /// </summary>
        public Dictionary<string, Package> FlatDependencies { get; } = new Dictionary<string, Package>();

        /// <summary></summary>
        /// <param name="meta">Package metadata associated</param>
        /// <param name="engine">The script engine associated</param>
        public Package(PackageMeta meta, IScriptEngine engine)
        {
            Meta = meta;
            Engine = engine;
            Log = this.GetContext();
        }

        /// <summary>
        /// Runs specified action of this package
        /// </summary>
        /// <param name="action"></param>
        /// <param name="recursive"></param>
        public void RunAction(string action, bool recursive = true)
        {
            if (DependencyTreeDepth == 0 && recursive)
            {
                foreach (var dependency in FlatDependencies.Values)
                {
                    dependency.RunAction(action);
                }
            }
            Engine.RunAction(this, action);
        }

        /// <summary>
        /// Instantiate the dependencies of this pack. Call this after
        /// successfully retrieved the initial pack.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Uppm doesn't support different versions of the same pack in
        /// the same dependency tree. There are couple of preferences when
        /// it comes to conflicting versions of the same pack which multiple
        /// other packages are depending on.
        /// <list type="bullet">
        /// <item><description>Only one dependency kept when both conflicting packs have different special versions</description></item>
        /// <item><description>Non-special version (`latest` or semantical) is preferred over special version</description></item>
        /// <item><description>More specific semantical version is preferred over `latest`</description></item>
        /// <item><description>The highest semantically versioned dependency is kept from conflicting ones</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public void ConstructDependencyTree()
        {
            Root.FlatDependencies.Clear();

            foreach (var depref in Meta.Dependencies)
            {
                if (!depref.TryGetPackage(out var dependency))
                {
                    Log.Warning(
                        "Couldn't get dependency {$DepPackRef} of {PackName}.\nSkipping dependency. Probably the package will have issues.",
                        depref, Meta.Name
                    );
                    continue;
                }
                if (Root.FlatDependencies.TryGetValue(depref.Name, out var rootdep))
                {
                    if (HandleConflictingSpecialVersions(dependency, rootdep)) continue;
                    if (HandleSpecialNonSpecialVersionConflict(dependency, rootdep)) continue;
                    if (HandleIsLatestMoreSpecific(dependency, rootdep)) continue;

                    // in case both are the latest prefer already existing
                    if (rootdep.Meta.IsLatest && dependency.Meta.IsLatest) continue;

                    HandleSemanticalVersionConflict(dependency, rootdep);
                }
                else
                {
                    Root.FlatDependencies.UpdateGeneric(dependency.Meta.Name, dependency);
                    ConstructDependencyTree(dependency);
                }
            }
        }

        private void ConstructDependencyTree(Package dependency)
        {
            dependency.Root = Root;
            dependency.DependencyTreeDepth = DependencyTreeDepth + 1;
            dependency.ConstructDependencyTree();
        }

        /// <summary>
        /// in case conflicting special versions prefer already existing
        /// </summary>
        /// <param name="current"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        private bool HandleConflictingSpecialVersions(Package current, Package existing)
        {
            if (current.Meta.IsSpecialVersion && existing.Meta.IsSpecialVersion)
            {
                if (!existing.Meta.Version.EqualsCaseless(current.Meta.Version))
                    LogDependencyVersionConflict(existing, current);
                return true;
            }
            return false;
        }

        /// <summary>
        /// in case conflicting special vs non-special version prefer non-special
        /// </summary>
        /// <param name="current"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        private bool HandleSpecialNonSpecialVersionConflict(Package current, Package existing)
        {
            if (existing.Meta.IsSpecialVersion != current.Meta.IsSpecialVersion)
            {
                var kept = existing.Meta.IsSpecialVersion ? current : existing;
                var skipped = existing.Meta.IsSpecialVersion ? existing : current;
                LogDependencyVersionConflict(kept, skipped);
                
                Root.FlatDependencies.UpdateGeneric(kept.Meta.Name, kept);

                if (existing.Meta.IsSpecialVersion) ConstructDependencyTree(current);
                return true;
            }
            return false;
        }

        /// <summary>
        /// In case conflicting `latest` prefer specific version
        /// </summary>
        /// <param name="current"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        private bool HandleIsLatestMoreSpecific(Package current, Package existing)
        {
            if (existing.Meta.IsLatest != current.Meta.IsLatest)
            {
                var kept = existing.Meta.IsLatest ? current : existing;
                LogDependencyLatestConflict(kept);
                
                Root.FlatDependencies.UpdateGeneric(kept.Meta.Name, kept);

                if(existing.Meta.IsLatest) ConstructDependencyTree(current);
                return true;
            }
            return false;
        }

        /// <summary>
        /// In case they are both semantical versions prefer higher
        /// </summary>
        /// <param name="current"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        private bool HandleSemanticalVersionConflict(Package current, Package existing)
        {
            if (existing.Meta.IsSemanticalVersion(out var rootver) &&
                current.Meta.IsSemanticalVersion(out var currver))
            {
                rootver.MissingInference = currver.MissingInference = UppmVersion.Inference.Newest;
                var kept = currver > rootver ? current : existing;
                var skipped = currver > rootver ? existing : current;
                if (currver.Major != rootver.Major || currver.Minor != rootver.Minor)
                {
                    LogDependencyMajorMinorConflict(kept, skipped);
                }
                Root.FlatDependencies.UpdateGeneric(kept.Meta.Name, kept);

                if(currver > rootver) ConstructDependencyTree(current);
                return true;
            }
            return false;
        }

        private void LogDependencyVersionConflict(Package kept, Package skipped)
        {
            Log.Warning(
@"Conflicting versions are referenced of the same package as dependencies by other packages: {KeptName}
    kept version    `{$KeptVersion}`
    skipped version `{$SkippedVersion}`

    Multiple package versions are not supported in uppm by design. Only one of them will be installed.
    This might be OK and best case scenario nothing will break.",
                kept.Meta.Name,
                kept.Meta.Version,
                skipped.Meta.Version
            );
        }

        private void LogDependencyLatestConflict(Package kept)
        {
            Log.Warning(
@"More specific version is referenced of the same package as dependencies by other packages: {KeptName}
    kept version `{$KeptVersion}`

    Multiple package versions are not supported in uppm by design. Only one of them will be installed.
    This might be OK and best case scenario nothing will break.",
                kept.Meta.Name,
                kept.Meta.Version
            );
        }

        private void LogDependencyMajorMinorConflict(Package kept, Package skipped)
        {
            Log.Warning(
@"Potentially incompatible versions are referenced of the same package as dependencies by other packages: {KeptName}
    kept higher version   `{$KeptVersion}`
    skipped lower version `{$SkippedVersion}`

    Multiple package versions are not supported in uppm by design. Only one of them will be installed.
    This might be OK and best case scenario nothing will break.",
                kept.Meta.Name,
                kept.Meta.Version,
                skipped.Meta.Version
            );
        }

        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);
    }
}