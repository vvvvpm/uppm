using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.String;
using uppm.Core.Utils;

namespace uppm.Core
{
    /// <summary>
    /// Parsed representation of an uppm package reference.
    /// </summary>
    /// <remarks>
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
    public class PackageReference
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
        public static PackageReference Parse(string refstring)
        {
            var crefstring = refstring;
            if (refstring.StartsWithCaseless(UppmUriScheme))
            {
                crefstring = Uri.UnescapeDataString(refstring);
                crefstring = crefstring.Replace(UppmUriScheme, "");
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
                    var res = new PackageReference
                    {
                        Name = name,
                        RepositoryUrl = regexmatches["repo"]?.Value,
                        PackVersion = regexmatches["version"]?.Value
                    };
                }
                else return null;

                // TODO modularly and nicely decide repo type
            }
            else return null;
        }

        /// <summary>
        /// Name of the referred package
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// Repository URL of the referred package
        /// </summary>
        public string RepositoryUrl { get; set; }
        
        /// <summary>
        /// Version of the referred package
        /// </summary>
        public string PackVersion { get; set; }

        /// <summary>
        /// Is the versioning of referred pack semantical?
        /// </summary>
        /// <param name="semversion">Parsed version</param>
        /// <returns>True if semantical</returns>
        public bool IsSemanticalVersion(out UppmVersion semversion)
        {
            var res = UppmVersion.TryParse(PackVersion, out semversion);
            if (!res && IsLatest)
            {
                semversion = new UppmVersion(int.MaxValue);
                res = true;
            }
            return res;
        }

        /// <summary>
        /// Is the versioning of referred pack is not semantical?
        /// </summary>
        public bool IsSpecialVersion => PackVersion != null && !IsSemanticalVersion(out _) && !IsLatest;

        /// <summary>
        /// Is the reference points to the latest version of the referred pack?
        /// </summary>
        public bool IsLatest => PackVersion != null && PackVersion.EqualsCaseless("latest");

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
            if (!string.IsNullOrWhiteSpace(PackVersion) && !string.IsNullOrWhiteSpace(other.PackVersion))
            {
                var iscurrsemver = IsSemanticalVersion(out var currsemversion);
                var isothersemversion = other.IsSemanticalVersion(out var othersemversion);

                if (iscurrsemver && isothersemversion)
                {
                    versionmatches = versionCompare(currsemversion, othersemversion);
                }
                else if (iscurrsemver == isothersemversion) // so both are false
                {
                    versionmatches = PackVersion.EqualsCaseless(other.PackVersion);
                }
            }
            else versionmatches = string.IsNullOrWhiteSpace(PackVersion) == string.IsNullOrWhiteSpace(other.PackVersion);

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
                       PackVersion.EqualsCaseless(other.PackVersion) &&
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
                hashCode = (hashCode * 397) ^ (PackVersion != null ? PackVersion.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"{Name}:{PackVersion}@{RepositoryUrl}";
    }
}
