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

        /// <summary>
        /// Event which can produce rapidly changing progress information
        /// which would potentially pollute or put unnecessary stress onto Serilog loggers.
        /// </summary>
        event UppmProgressHandler OnProgress;

        /// <summary>
        /// Invoke any progress change. This is only meant to be used internally by uppm
        /// via the extension method <see cref="UppmLog.InvokeAnyProgress"/>
        /// </summary>
        /// <param name="progress"></param>
        void InvokeProgress(ProgressEventArgs progress);
    }

    /// <summary>
    /// Event arguments for average progress report
    /// </summary>
    public struct ProgressEventArgs
    {
        /// <summary></summary>
        public double TotalValue;

        /// <summary></summary>
        public double CurrentValue;

        /// <summary>
        /// What is currently done?
        /// </summary>
        public string State;

        /// <summary>
        /// A message which is changing rapidly for example a currently processed item
        /// </summary>
        public string Message;

        /// <summary>
        /// Convenience property to have 0.0 .. 1.0 normalized range of the progress. -1 if it's not ranged
        /// </summary>
        public double NormalizedProgress => TotalValue > 0 ? CurrentValue / TotalValue : -1;

        /// <summary>
        /// Total steps/items/time etc is known
        /// </summary>
        public bool IsTotalKnown => TotalValue > 0;

        /// <summary>
        /// Represented operation can go on indefinitely
        /// </summary>
        public bool IsTotalUnknown => TotalValue <= 0;

        /// <summary></summary>
        /// <param name="totalValue"></param>
        /// <param name="currentValue"></param>
        /// <param name="state"></param>
        /// <param name="message"></param>
        public ProgressEventArgs(double totalValue = 0, double currentValue = 0, string state = "", string message = "")
        {
            TotalValue = totalValue;
            CurrentValue = currentValue;
            State = state;
            Message = message;
        }
    }

    public delegate void UppmProgressHandler(ILogSource sender, ProgressEventArgs args);

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
        /// <param name="configure">Optional configuration delegate for a logger</param>
        /// <param name="subscriber">Optional subscription delegate for an observer. `Next` is called on each logged entry.</param>
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

        /// <summary>
        /// Invoke any progress change
        /// </summary>
        /// <param name="source"></param>
        /// <param name="totalValue"></param>
        /// <param name="currentValue"></param>
        /// <param name="state"></param>
        /// <param name="message"></param>
        public static void InvokeAnyProgress(this ILogSource source, double totalValue = 0, double currentValue = 0, string state = "", string message = "")
        {
            var prog = new ProgressEventArgs(totalValue, currentValue, state, message);
            source.InvokeProgress(prog);
            OnAnyProgress?.Invoke(source, prog);
        }

        /// <summary>
        /// A global event which can produce rapidly changing progress information
        /// which would potentially pollute or put unnecessary stress onto Serilog loggers.
        /// </summary>
        public static event UppmProgressHandler OnAnyProgress;
    }
}
