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
    }

    /// <summary>
    /// Default implementation of <see cref="IUppmImplementation"/> in case the uppm.Core
    /// implementation doesn't specify one.
    /// </summary>
    public class UnknownUppmImplementation : IUppmImplementation
    {
        /// <inheritdoc />
        public string Exe { get; } = Assembly.GetExecutingAssembly().Location;

        /// <inheritdoc />
        public string TemporaryFolder { get; } = Path.GetTempPath();
    }
}
