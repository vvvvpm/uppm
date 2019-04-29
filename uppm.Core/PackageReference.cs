using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using md.stdl.String;
using uppm.Core.Repositories;
using uppm.Core.Utils;

namespace uppm.Core
{
    /// <summary>
    /// Parsed representation of an uppm package reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note for safety there are separate types for user input references which might be incomplete
    /// <see cref="PartialPackageReference"/> and complete references <see cref="CompletePackageReference"/>
    /// where which it can be safely assumed that no further data needs to be infered.
    /// </para>
    /// <para>Uppm has its own package reference format used for dependencies and meta or script imports:</para>
    /// <code>
    /// &lt;name&gt;:&lt;version&gt; @ &lt;repository&gt;
    /// </code>
    /// <para>
    /// Only name is required and the rest can be inferred. No text in any of the reference parts are case
    /// sensitive so `MYPACK` == `mypack`. If version is not specified then either the highest semantical
    /// version is selected or the package versioned `latest`. If none of those exists uppm will throw a
    /// package not found error. Version can be semantical but it can be any other string. Non-semantically
    /// versioned packages are never infered (except `latest`).
    /// </para>
    /// <para>Read more in README.md</para>
    /// </remarks>
    public abstract class PackageReference : Versionable
    {
        /// <summary>
        /// URI scheme to be used for uppm references sent from external sources
        /// </summary>
        public const string UppmUriScheme = "uppm-ref:";

        /// <summary>
        /// Create a reference out of a string
        /// </summary>
        /// <param name="refstring"></param>
        /// <returns></returns>
        public static PartialPackageReference Parse(string refstring)
        {
            var crefstring = refstring;
            string targetapp = null;
            if (refstring.StartsWithCaseless(UppmUriScheme))
            {
                crefstring = Uri.UnescapeDataString(refstring);
                var rgx = new Regex($@"{Regex.Escape(UppmUriScheme)}(?<app>\w+):");
                var matches = rgx.Match(crefstring);
                if (matches.Success)
                {
                    targetapp = matches.Groups["app"].Value;
                    crefstring = rgx.Replace(crefstring, "");
                }
                else
                {
                    Logging.L.Error(
                        "Cannot parse {Url} as package reference because it has invalid format",
                        refstring
                    );
                    return null;
                }
            }
            var regexmatches = refstring.MatchGroup(
                @"^(?<name>[^@\:]+?)" + // name
                @"(\s*?\:\s*?(?<version>[^@\:]+?))?" + // version
                @"(\s*?@\s*?(?<repo>\S+?))?$"); // repository
            if (regexmatches != null && regexmatches.Count > 0)
            {
                var name = regexmatches["name"]?.Value;
                if (name != null)
                {
                    return new PartialPackageReference
                    {
                        Name = name,
                        RepositoryUrl = regexmatches["repo"]?.Value,
                        Version = regexmatches["version"]?.Value,
                        TargetApp = targetapp
                    };
                }
                else return null;
            }
            else return null;
        }

        /// <summary>
        /// Name of the referred package
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optionally specify target application if user input is coming from uppm-ref URI
        /// </summary>
        public string TargetApp { get; set; }

        /// <summary>
        /// Repository URL of the referred package
        /// </summary>
        public string RepositoryUrl { get; set; }

        /// <summary>
        /// Is this reference matches another one.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="versionCompare">A comparison can be specified. Default is <see cref="UppmVersion.Comparison.Same"/></param>
        /// <returns></returns>
        public bool Matches(PackageReference other, UppmVersion.VersionCompare versionCompare = null)
        {
            if (other == null) return false;

            versionCompare = versionCompare ?? UppmVersion.Comparison.Same;

            var versionmatches = false;
            if (!string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(other.Version))
            {
                var iscurrsemver = IsSemanticalVersion(out var currsemversion);
                var isothersemversion = other.IsSemanticalVersion(out var othersemversion);

                if (iscurrsemver && isothersemversion)
                {
                    versionmatches = versionCompare(currsemversion, othersemversion);
                }
                else if (iscurrsemver == isothersemversion) // so both are false
                {
                    versionmatches = Version.EqualsCaseless(other.Version);
                }
            }
            else versionmatches = string.IsNullOrWhiteSpace(Version) == string.IsNullOrWhiteSpace(other.Version);

            var repositorymatches = false;
            if (RepositoryUrl != null && other.RepositoryUrl != null)
            {
                repositorymatches = RepositoryUrl.EqualsCaseless(other.RepositoryUrl);
            }
            else repositorymatches = (RepositoryUrl == null) == (other.RepositoryUrl == null);

            return Name.EqualsCaseless(other.Name) && repositorymatches && versionmatches;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is PackageReference other)
            {
                return (Name?.EqualsCaseless(other.Name) ?? false) &&
                       Version.EqualsCaseless(other.Version) &&
                       (RepositoryUrl?.EqualsCaseless(other.RepositoryUrl) ?? false);
            }
            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RepositoryUrl != null ? RepositoryUrl.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"{Name}:{Version}@{RepositoryUrl}";
    }

    /// <inheritdoc />
    public class PartialPackageReference : PackageReference { }

    /// <inheritdoc />
    /// <summary>
    /// A complete reference to a package, meaning both version and repository
    /// is precisely specified and nothing has to be further infered
    /// </summary>
    public class CompletePackageReference : PartialPackageReference { }
}
