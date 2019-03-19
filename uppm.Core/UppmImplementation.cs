using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace uppm.Core
{
    /// <summary>
    /// Contains implementation specific information about the executable using uppm.Core
    /// </summary>
    public interface IUppmImplementation
    {
        /// <summary>
        /// Path to the package manager executable
        /// </summary>
        string Exe { get; }

        /// <summary>
        /// Path to the temporary work folder
        /// </summary>
        string TemporaryFolder { get; }

        /// <summary>
        /// Short name of the implementation.
        /// </summary>
        string ShortName { get; }
    }

    /// <summary>
    /// Simple base class for uppm implementations
    /// </summary>
    public abstract class UppmImplementation : IUppmImplementation
    {
        /// <inheritdoc />
        public virtual string Exe => Assembly.GetExecutingAssembly().Location;

        /// <inheritdoc />
        public virtual string TemporaryFolder => Path.Combine(Path.GetTempPath(), $".{this.GetSystemName()}");

        /// <inheritdoc />
        public abstract string ShortName { get; }
    }

    public static class Uppm
    {
        /// <summary>
        /// Separator to be used for the system wide short name of a given uppm implementation
        /// </summary>
        public const string SystemNameSeparator = "-";

        private static string _workingDirectory = Environment.CurrentDirectory;
        
        /// <summary>
        /// Gets the name which will be used system wide to call or manage given implementation of uppm
        /// </summary>
        /// <returns></returns>
        public static string GetSystemName(this IUppmImplementation impl)
        {
            return string.IsNullOrWhiteSpace(impl.ShortName) ? "uppm" : $"uppm{SystemNameSeparator}{impl.ShortName}";
        }

        /// <summary>
        /// Current implementer of Uppm
        /// </summary>
        public static IUppmImplementation Implementation { get; set; }

        /// <summary>
        /// Currently targeted application
        /// </summary>
        public static TargetApplication CurrentApplication { get; set; }

        /// <summary>
        /// Overridable synonim to <see cref="Environment.CurrentDirectory"/>
        /// </summary>
        public static string WorkingDirectory
        {
            get => _workingDirectory;
            set
            {
                var prevworkdir = _workingDirectory;
                if(!Directory.Exists(value))
                {
                    UppmLog.L.Warning("Trying to override the working directory with one which doesn't exist. Creating it.");
                    try
                    {
                        Directory.CreateDirectory(value);
                    }
                    catch (Exception e)
                    {
                        UppmLog.L.Error("Overriding working directory failed, previously set or default is used.");
                        return;
                    }
                }
                _workingDirectory = value;
            }
        }

        /// <summary>
        /// Version of the currently loaded uppm.Core
        /// </summary>
        public static UppmVersion CoreVersion { get; } = new UppmVersion(typeof(UppmVersion).Assembly.GetName().Version);
    }

    /// <summary>
    /// Default implementation of <see cref="IUppmImplementation"/> in case the uppm.Core
    /// implementation doesn't specify one.
    /// </summary>
    public class UnknownUppmImplementation : UppmImplementation
    {
        public override string ShortName => "";
    }
}
