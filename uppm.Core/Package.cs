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
    public enum ExistingPackageResolution
    {
        NonExisting,
        Skip,
        UpdateWithInput,
        UpdateWithExisting
    }

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
    public class Package : ILogging
    {
        private Package _root;
        private InstalledPackageScope _scope;

        /// <summary>
        /// Metadata of the package
        /// </summary>
        public PackageMeta Meta { get; }

        /// <summary>
        /// Associated script engine with the package in runtime
        /// </summary>
        public IScriptEngine Engine { get; }

        /// <summary>
        /// The selected target application of this package
        /// </summary>
        public TargetApp TargetApplication { get; set; }

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
        /// The target scope this package is supposed to be located after installation.
        /// </summary>
        public InstalledPackageScope Scope
        {
            get => Meta.ForceGlobal ? InstalledPackageScope.Global : _scope;
            set => _scope = value;
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
        /// <param name="confirmLicenseWithUser">Ask user to agree to package licenses</param>
        /// <returns>True if action ran without errors</returns>
        public bool RunAction(string action, bool recursive = true, bool confirmLicenseWithUser = true)
        {
            var prevtarget = TargetApp.CurrentTargetApp;
            if (!TargetApp.TrySetCurrentApp(Meta.TargetApp, out var currtarget))
            {
                Log.Error("Package targets an unknown application. `{Action}` Action won't run.", action);
                return false;
            }
            TargetApplication = currtarget;

            bool result = true;

            if (DependencyTreeDepth == 0 && recursive)
            {
                var isInstall = action.EqualsCaseless("install");

                if (FlatDependencies.Count == 0)
                    ConstructDependencyTree();

                if (isInstall && confirmLicenseWithUser)
                {
                    Log.Information("In order to use this package and its dependencies please accept these licenses:");
                    LogLicense();
                    foreach (var dep in FlatDependencies.Values)
                    {
                        dep.LogLicense();
                    }
                    Log.Information(Meta.License);
                    result = Logging.ConfirmWithUser("Do you agree to these terms?", source: this);
                }

                if(result)
                {
                    foreach (var dependency in FlatDependencies.Values)
                    {
                        result = result && dependency.RunAction(action, true, confirmLicenseWithUser);
                        if (!result)
                        {
                            Log.Error(
                                "Dependency {$DepPackRef} of {$CurrPackRef} couldn't execute `{Action}`",
                                dependency.Meta.Self,
                                Meta.Self,
                                action
                            );
                        }
                    }
                }
            }

            if (result)
            {
                Log.Information("Executing `{Action}` of {$PackRef}");
            }

            result = result && Engine.RunAction(this, action);
            prevtarget.SetAsCurrentApp();
            return result;
        }

        public void LogLicense()
        {
            Log.Information("License of {$PackRef}:", Meta.Self);
            Log.Information(Meta.License);
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
            if (DependencyTreeDepth == 0)
            {
                FlatDependencies.Clear();
            }

            foreach (var idepref in Meta.Dependencies)
            {
                var depref = idepref;
                var existingResolution = ResolveExistingPackage(ref depref, out var dependency);
                switch (existingResolution)
                {
                    case ExistingPackageResolution.Skip:
                        Log.Information(
                            "Skip dependency because a more appropriate or a newer version is already installed.",
                            depref
                        );
                        break;
                        continue;

                    case ExistingPackageResolution.UpdateWithInput:
                        Log.Information(
                            "Existing package will be overwritten.",
                            depref
                        );
                        break;

                    case ExistingPackageResolution.UpdateWithExisting:
                        Log.Information(
                            "Dependency version {DepVer} is overridden in favor of updating to a newer version of the same pack {NewDepVer},\n    because a package referring to a higher version scope or to the latest",
                            idepref.Version,
                            depref.Version
                        );
                        break;
                }

                if (!depref.TryGetPackageFromRepositories(out dependency))
                {
                    Log.Warning(
                        "Couldn't get dependency {$DepPackRef} of {PackName}.\nSkipping dependency. Probably the package will have issues.",
                        depref, Meta.Name
                    );
                    continue;
                }
                if (Root.FlatDependencies.TryGetValue(depref.Name, out var rootdep))
                {
                    if (HandleConflictingSpecialVersions(dependency.Meta.Self, rootdep.Meta.Self)) continue;
                    if (HandleSpecialNonSpecialVersionConflict(dependency, rootdep)) continue;
                    if (HandleIsLatestMoreSpecific(dependency, rootdep)) continue;

                    // in case both are the latest prefer already existing
                    if (rootdep.Meta.IsLatest && dependency.Meta.IsLatest) continue;

                    dependency.Scope = Scope;

                    HandleSemanticalVersionConflict(dependency, rootdep);
                }
                else
                {
                    dependency.Scope = Scope;
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
        /// Prints a treeview representation of the dependency tree
        /// </summary>
        /// <returns></returns>
        public string PrintDependencyTree()
        {
            if (FlatDependencies.Count == 0)
                ConstructDependencyTree();

            var res = Meta.Self.ToString();
            foreach (var dep in FlatDependencies.Values)
            {
                res += Environment.NewLine;
                res += new string('X', dep.DependencyTreeDepth).Replace("X", "  ");
                res += dep.Meta.Self.ToString();
            }
            return res;
        }

        private ExistingPackageResolution ResolveExistingPackage(ref PartialPackageReference packref, out Package existingPackage)
        {
            existingPackage = null;
            Log.Verbose(
                "Check {$DepPackRef} if it's already installed.",
                packref
            );
            if (TargetApp.CurrentTargetApp.TryGetInstalledPackage(packref, Scope, out existingPackage))
            {
                Log.Information(
                    "Package {$DepPackRef} is already installed.",
                    packref
                );
                if (packref.Version.EqualsCaseless(existingPackage.Meta.Version))
                {
                    Log.Information("Exact same version installed, Skipping");
                    return ExistingPackageResolution.Skip;
                }

                if (HandleConflictingSpecialVersions(packref, existingPackage.Meta.Self))
                    return ExistingPackageResolution.Skip;


                if (existingPackage.Meta.IsLatest && !packref.IsSpecialVersion)
                {
                    if (Logging.ConfirmWithUser(
                        "Existing package marked to be always the latest version (including Major version). Do you want to update it?",
                        source: this
                    )) {
                        packref = existingPackage.Meta.Self;
                        return ExistingPackageResolution.UpdateWithExisting;
                    }
                    else return ExistingPackageResolution.Skip;
                }


                if (existingPackage.Meta.IsSemanticalVersion(out var existingVersion) && packref.IsSemanticalVersion(out var depVersion))
                {
                    if (existingVersion == depVersion)
                    {
                        Log.Information("Exact same version installed, Skipping");
                        return ExistingPackageResolution.Skip;
                    }

                    if (depVersion > existingVersion)
                    {
                        if (Logging.ConfirmWithUser("Already installed pack is a lower version. Do you want to overwrite it?", source: this))
                            return ExistingPackageResolution.UpdateWithInput;
                        else return ExistingPackageResolution.Skip;
                    }

                    if (existingVersion.Major > depVersion.Major)
                    {
                        LogDependencyMajorMinorConflict(existingPackage.Meta.Self, packref);
                        return ExistingPackageResolution.Skip;
                    }

                    var maxscope = Math.Min((int)existingVersion.Scope, (int)depVersion.Scope);

                    if (existingVersion.Scope < depVersion.Scope)
                    {
                        Log.Information(
                            "Installed package points to the latest version of its scope: {ExistingScope} scope instead of the input package's {DepScope} scope.",
                            existingVersion.Scope,
                            depVersion.Scope
                        );

                        if (Logging.ConfirmWithUser(
                            "Do you want to update it?",
                            source: this
                        ))
                        {
                            packref = existingPackage.Meta.Self;
                            return ExistingPackageResolution.UpdateWithExisting;
                        }
                        else return ExistingPackageResolution.Skip;
                    }
                }

                return ExistingPackageResolution.UpdateWithInput;
            }
            else return ExistingPackageResolution.NonExisting;
        }

        /// <summary>
        /// in case conflicting special versions prefer already existing
        /// </summary>
        /// <param name="current"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        private bool HandleConflictingSpecialVersions(PackageReference current, PackageReference existing)
        {
            if (current.IsSpecialVersion && existing.IsSpecialVersion)
            {
                if (!existing.Version.EqualsCaseless(current.Version))
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
                LogDependencyVersionConflict(kept.Meta.Self, skipped.Meta.Self);
                
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
                LogDependencyLatestConflict(kept.Meta.Self);
                
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
                    LogDependencyMajorMinorConflict(kept.Meta.Self, skipped.Meta.Self);
                }
                Root.FlatDependencies.UpdateGeneric(kept.Meta.Name, kept);

                if(currver > rootver) ConstructDependencyTree(current);
                return true;
            }
            return false;
        }

        private void LogDependencyVersionConflict(PackageReference kept, PackageReference skipped)
        {
            Log.Warning(
@"Trying to install conflicting versions of the same package: {KeptName}
    kept version    `{$KeptVersion}`
    skipped version `{$SkippedVersion}`

    Multiple package versions are not supported in uppm by design. Only one of them will be installed.
    This might be OK and best case scenario nothing will break.",
                kept.Name,
                kept.Version,
                skipped.Version
            );
        }

        private void LogDependencyLatestConflict(PackageReference kept)
        {
            Log.Warning(
@"Trying to install both latest and a more specific version of the same package: {KeptName}
    kept version `{$KeptVersion}`

    Multiple package versions are not supported in uppm by design. Only one of them will be installed.
    This might be OK and best case scenario nothing will break.",
                kept.Name,
                kept.Version
            );
        }

        private void LogDependencyMajorMinorConflict(PackageReference kept, PackageReference skipped)
        {
            Log.Warning(
@"Trying to install potentially incompatible versions of the same package: {KeptName}
    kept higher version   `{$KeptVersion}`
    skipped lower version `{$SkippedVersion}`

    Multiple package versions are not supported in uppm by design. Only one of them will be installed.
    This might be OK and best case scenario nothing will break.",
                kept.Name,
                kept.Version,
                skipped.Version
            );
        }

        public ILogger Log { get; }

        /// <inheritdoc />
        public event UppmProgressHandler OnProgress;

        /// <inheritdoc />
        public void InvokeProgress(ProgressEventArgs progress) => OnProgress?.Invoke(this, progress);
    }
}