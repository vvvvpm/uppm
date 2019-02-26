using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace uppm.Core.Utils
{
    /// <summary>
    /// Used for determining minimum version of uppm given entity is compatible with
    /// </summary>
    public struct VersionRequirement
    {
        /// <summary>
        /// Getting version requirement was successful
        /// </summary>
        public bool Valid;

        /// <summary>
        /// Minimal version of uppm required for associated entity
        /// </summary>
        public UppmVersion MinimalVersion;

        /// <summary>
        /// True if associated entity is compatible with currently running uppm
        /// </summary>
        public bool VersionValid => MinimalVersion <= UppmVersion.CoreVersion;
    }

    /// <summary>
    /// Semantical versioning with scope awareness
    /// </summary>
    /// <remarks>
    /// <para>
    /// UppmVersion also introduces some more intelligent handling of missing semantical components.
    /// It introduces version scopes where the higher version components encapsulate all versions
    /// specifying lower version components, if/when they are not explicitly specified in the first place.
    /// </para>
    /// <para>
    /// Example: By default, any version Major.Minor.Build.Rev will be smaller than Major.Minor.Build
    /// which all will be smaller than Major.Minor and the single Major number will be larger than all
    /// of those. Possibly missing and therefore infered semantical components are represented as
    /// nullable integers.
    /// </para>
    /// </remarks>
    public struct UppmVersion
    {
        /// <summary>
        /// Biggest scope this version represents
        /// </summary>
        public enum SemanticScope
        {
            /// <summary>
            /// Major only (single number)
            /// </summary>
            Major,

            /// <summary>
            /// Major.Minor
            /// </summary>
            Minor,

            /// <summary>
            /// Major.Minor.Build
            /// </summary>
            Build,

            /// <summary>
            /// Major.Minor.Build.Rev
            /// </summary>
            Revision,
        }

        /// <summary>
        /// Delegate type for inferring missing semantical components
        /// </summary>
        /// <param name="v">Individual component</param>
        /// <returns>Inferred component</returns>
        public delegate int MissingInferenceDelegate(int? v);

        /// <summary>
        /// Collection of commonly used missing semantical component inference
        /// </summary>
        public static class Inference
        {
            /// <summary>Missing component should always point to newest version</summary>
            public static readonly MissingInferenceDelegate Newest = v => v ?? int.MaxValue;

            /// <summary>Missing component should always point to 0</summary>
            public static readonly MissingInferenceDelegate Zero = v => v ?? 0;
        }

        /// <summary>
        /// Delegate for version comparison
        /// </summary>
        /// <param name="a">A</param>
        /// <param name="b">B</param>
        /// <returns></returns>
        public delegate bool VersionCompare(UppmVersion a, UppmVersion b);

        /// <summary>
        /// Collection of common version comparison methods
        /// </summary>
        public static class Comparison
        {
            /// <summary>
            /// Versions are equals
            /// </summary>
            public static readonly VersionCompare Same = (a, b) => a == b;

            /// <summary>
            /// A is older than B
            /// </summary>
            public static readonly VersionCompare Older = (a, b) => a < b;

            /// <summary>
            /// A is newer than B
            /// </summary>
            public static readonly VersionCompare Newer = (a, b) => a > b;

            /// <summary>
            /// A is equals or older than B
            /// </summary>
            public static readonly VersionCompare SameOrOlder = (a, b) => a <= b;

            /// <summary>
            /// A is equals or newer than B
            /// </summary>
            public static readonly VersionCompare SameOrNewer = (a, b) => a >= b;
        }

        /// <summary>
        /// Version of the currently loaded uppm.Core
        /// </summary>
        public static UppmVersion CoreVersion { get; } = new UppmVersion(typeof(UppmVersion).Assembly.GetName().Version);

        /// <summary>
        /// Try parsing a string as <see cref="UppmVersion"/>
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="version">Output version if successful, 0 Major if not</param>
        /// <param name="inference">Missing semantical component inference method</param>
        /// <returns>Wether parsing was successful or not</returns>
        public static bool TryParse(string input, out UppmVersion version, MissingInferenceDelegate inference = null)
        {
            version = new UppmVersion(0, inference: inference);
            var regex = Regex.Match(input, @"^(?<major>\d+?)(\.|$)(?<minor>\d+?)?(\.|$)(?<build>\d+?)?(\.|$)(?<rev>\d+?)?$");
            if (!regex.Success) return false;
            version.Major = Int32.TryParse(regex.Groups["major"].Value, out var major) ? major : 0;
            version.Minor = Int32.TryParse(regex.Groups["minor"].Value, out var minor) ? new int?(minor) : null;
            version.Build = Int32.TryParse(regex.Groups["build"].Value, out var build) ? new int?(build) : null;
            version.Revision = Int32.TryParse(regex.Groups["rev"].Value, out var rev) ? new int?(rev) : null;
            return true;
        }

        private int _major;
        private int? _minor;
        private int? _build;
        private int? _revision;

        /// <summary>
        /// Semantical components as an array
        /// </summary>
        public int[] Components;

        /// <summary>
        /// Missing semantical component inference method
        /// </summary>
        public MissingInferenceDelegate MissingInference;

        /// <summary>
        /// The semantical scope of this version
        /// </summary>
        public SemanticScope Scope
        {
            get
            {
                if (_minor == null) return SemanticScope.Major;
                if (_build == null) return SemanticScope.Minor;
                if (_revision == null) return SemanticScope.Build;
                return SemanticScope.Revision;
            }
        }

        /// <summary>
        /// Huge breaking changes, totally different software. <see cref="UppmVersion"/> must have
        /// the Major component specified
        /// </summary>
        public int Major
        {
            get => _major;
            set
            {
                _major = value;
                Components[0] = value;
            }
        }

        /// <summary>
        /// Possibly breaking changes, substential improvements
        /// </summary>
        public int? Minor
        {
            get => _minor;
            set
            {
                _minor = value;
                Components[1] = MissingInference(value);
            }
        }

        /// <summary>
        /// Bugfixes, little improvements, shouldn't be breaking
        /// </summary>
        public int? Build
        {
            get => _build;
            set
            {
                _build = value;
                Components[2] = MissingInference(value);
            }
        }

        /// <summary>
        /// Minor bugfixes, almost unnoticable improvements, breaking deserves a facepalm
        /// </summary>
        public int? Revision
        {
            get => _revision;
            set
            {
                _revision = value;
                Components[3] = MissingInference(value);
            }
        }

        /// <summary>
        /// Construct an <see cref="UppmVersion"/> out of a <see cref="Version"/>
        /// </summary>
        /// <param name="v"></param>
        public UppmVersion(Version v)
        {
            _major = v.Major;
            _minor = v.Minor;
            _build = v.Build;
            _revision = v.Revision;
            MissingInference = Inference.Zero;

            Components = new[]
            {
                _major,
                MissingInference(_minor),
                MissingInference(_build),
                MissingInference(_revision)
            };
        }

        /// <summary>
        /// Construct a version out of individual components
        /// </summary>
        /// <param name="major"></param>
        /// <param name="minor"></param>
        /// <param name="build"></param>
        /// <param name="revision"></param>
        /// <param name="inference">Missing semantical component inference method</param>
        public UppmVersion(int major, int? minor = null, int? build = null, int? revision = null, MissingInferenceDelegate inference = null) : this()
        {
            _major = major;
            _minor = minor;
            _build = build;
            _revision = revision;
            MissingInference = inference ?? Inference.Newest;

            Components = new[]
            {
                _major,
                MissingInference(_minor),
                MissingInference(_build),
                MissingInference(_revision)
            };
        }

        /// <summary>
        /// Creates a traditional <see cref="Version"/> out of this <see cref="UppmVersion"/>
        /// </summary>
        /// <returns></returns>
        public Version ToVersion() => new Version(
            _major,
            MissingInference(_minor),
            MissingInference(_build),
            MissingInference(_revision)
        );

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return obj is UppmVersion other && other.ToVersion().Equals(ToVersion());
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _major ^ MissingInference(_minor) ^ MissingInference(_build) ^ MissingInference(_revision);
        }

        public static UppmVersion operator +(UppmVersion a, UppmVersion b) => new UppmVersion(
            a._major + b._major,
            (a._minor ?? 0) + b._minor,
            (a._build ?? 0) + b._build,
            (a._revision ?? 0) + b._revision
        );

        public static UppmVersion operator -(UppmVersion a, UppmVersion b) => new UppmVersion(
            a._major + b._major,
            (a._minor ?? 0) + b._minor,
            (a._build ?? 0) + b._build,
            (a._revision ?? 0) + b._revision
        );

        public static bool operator <(UppmVersion a, UppmVersion b) => a.ToVersion() < b.ToVersion();
        public static bool operator >(UppmVersion a, UppmVersion b) => a.ToVersion() > b.ToVersion();
        public static bool operator <=(UppmVersion a, UppmVersion b) => a.ToVersion() <= b.ToVersion();
        public static bool operator >=(UppmVersion a, UppmVersion b) => a.ToVersion() >= b.ToVersion();
        public static bool operator ==(UppmVersion a, UppmVersion b) => a.ToVersion() == b.ToVersion();
        public static bool operator !=(UppmVersion a, UppmVersion b) => a.ToVersion() != b.ToVersion();
    }
}
