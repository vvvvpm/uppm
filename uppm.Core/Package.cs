using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hjson;
using md.stdl.String;
using Newtonsoft.Json.Linq;
using uppm.Core.Utils;

namespace uppm.Core
{
    /// <summary>
    /// Immutable metadata for an opened package
    /// </summary>
    public class PackageMeta
    {
        public static void ParseFromHjson(string hjson, ref PackageMeta packmeta)
        {
            packmeta = packmeta ?? new PackageMeta();
            var json = HjsonValue.Parse(hjson).ToString();
            var jobj = packmeta.MetaData = JObject.Parse(json);

            packmeta.Name = jobj["name"].ToString();
            packmeta.Version = jobj["version"].ToString();

            packmeta.Author = jobj["author"]?.ToString();
            packmeta.License = jobj["license"]?.ToString();
            packmeta.ProjectUrl = jobj["projectUrl"]?.ToString();
            packmeta.Repository = jobj["repository"]?.ToString();
            packmeta.InferSelf();
            packmeta.Dependencies.Clear();

            if (jobj.TryGetValue("dependencies", out var jdeps))
            {
                foreach (var jdep in jdeps)
                {
                    packmeta.Dependencies.Add(PackageReference.Parse(jdep.ToString()));
                }
            }
        }

        /// <summary>
        /// The <see cref="JObject"/> representing the package descriptor comment in the package file
        /// </summary>
        public JObject MetaData { get; set; }

        /// <summary>
        /// Reference to self
        /// </summary>
        public PackageReference Self { get; set; }

        /// <summary>
        /// Required uppm version for this pack
        /// </summary>
        public VersionRequirement RequiredUppmVersion { get; set; }

        /// <summary>
        /// Friendly name of this package
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional author of this package
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Optional url to the package's project website
        /// </summary>
        public string ProjectUrl { get; set; }

        /// <summary>
        /// Optional license text
        /// </summary>
        public string License { get; set; }

        /// <summary>
        /// Arbitrary version text
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A package can optionally specify a repository explicitly,
        /// however this is only taken into account when a package is processed out of context.
        /// </summary>
        public string Repository { get; set; }

        /// <summary>
        /// Semantical version in case it's a valid one, otherwise null
        /// </summary>
        public UppmVersion? SemanticVersion { get; set; }

        /// <summary>
        /// The entire text of the package file
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// References for packages this one is depending on
        /// </summary>
        public List<PackageReference> Dependencies { get; } = new List<PackageReference>();

        /// <summary>
        /// References for packages which scripts should be imported into the current script.
        /// </summary>
        public List<PackageReference> Imports { get; } = new List<PackageReference>();

        public void InferSelf()
        {
            if (Self == null)
            {
                Self = new PackageReference
                {
                    Name = Name,
                    PackVersion = Version,
                    RepositoryUrl = Repository
                };
            }
        }
    }

    /// <summary>
    /// Actual package containing side effects and practical utilities
    /// </summary>
    public class Package
    {
        public struct GetMetaCommentResult
        {
            public bool Valid;
            public UppmVersion MinimalVersion;

            public bool VersionValid => MinimalVersion <= UppmVersion.CoreVersion;
        }

        public static bool TryGetUppmMetaComment(string packtext, out string metatext, out GetMetaCommentResult result)
        {
            var matches = packtext.MatchGroup(
                @"\A\/\*\s+uppm\s+(?<pmversion>[\d\.]+)\s+(?<packmeta>\{.*\})\s+\*\/",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
            metatext = "";
            result = new GetMetaCommentResult();
            if (matches == null ||
                matches.Count == 0 ||
                matches["pmversion"]?.Length <= 0 ||
                matches["packmeta"]?.Length <= 0
                )
                return false;

            if (UppmVersion.TryParse(matches["pmversion"].Value, out var minuppmversion, UppmVersion.Inference.Zero))
            {
                result.MinimalVersion = minuppmversion;
                result.Valid = minuppmversion <= UppmVersion.CoreVersion;
                metatext = matches["packmeta"].Value;
                return result.Valid;
            }
            else return false;
        }

        public PackageMeta Meta { get; set; }

        public ConcurrentDictionary<string, Package> Dependencies { get; } = new ConcurrentDictionary<string, Package>();
    }
}

