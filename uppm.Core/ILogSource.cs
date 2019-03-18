using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Enricher;
using Serilog.Events;

//TODO: adopt to Serilog

namespace uppm.Core
{
    /// <summary>
    /// Inheriting class can easily provide its own context while logging
    /// </summary>
    public interface ILogSource
    {
        /// <summary>
        /// The logger providing the context to the inheriting class
        /// </summary>
        /// <remarks>
        /// It is recommended to assign this autoproperty <see cref="UppmLog.GetContext"/> in the constructor.
        /// </remarks>
        ILogger Log { get; }
    }

    /// <summary>
    /// Utilities for logging
    /// </summary>
    public static class UppmLog
    {
        /// <summary>
        /// Uppm implementation have to at least initialize a default Serilog logger
        /// which then uppm can use. Implementation can also specify a function where
        /// the configuration can be extended or completely overriden.
        /// </summary>
        public static void ConfigureLogger(
            Func<LoggerConfiguration, LoggerConfiguration> configure = null,
            Action<IObservable<LogEvent>> subscriber = null
            )
        {
            var conf = new LoggerConfiguration().WriteTo.Observers(subscriber);
            if(configure != null) conf = configure(conf);
            L = conf.CreateLogger();
        }

        /// <summary>
        /// Uppm uses its own static logger instance so it won't "steal" the <see cref="Log.Logger"/> from the user
        /// </summary>
        public static Logger L { get; private set; }

        /// <summary>
        /// Shortcut to getting a consistent "UppmSource" property when <see cref="ILogSource"/> objects log.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static ILogger GetContext(this ILogSource source)
        {
            return L.ForContext("UppmSource", source);
        }
    }
}
