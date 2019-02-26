using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Core;

namespace uppm.Core
{
    /// <summary>
    /// Inheriting classes would like to log stuff.
    /// </summary>
    public interface ILogSource
    {
        /// <summary>
        /// Assign this property your logger. All logs are being sent via an <see cref="UppmLogEntry"/> object.
        /// </summary>
        ILog Log { get; }
    }

    /// <summary>
    /// Helper class for log entries which can carry extra information
    /// </summary>
    public class UppmLogEntry
    {
        /// <summary>
        /// Log message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Exception on Error or Critical
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Arbitrary object logged this one
        /// </summary>
        public object Source { get; }

        /// <summary>
        /// some more arbitrary information
        /// </summary>
        public dynamic Meta { get; set; }

        /// <inheritdoc />
        public override string ToString() => "uppm log entry: " + Message;

        /// <summary></summary>
        public Level Level { get; set; } = Level.Info;

        /// <summary></summary>
        /// <param name="m">Log message</param>
        /// <param name="src">optional source object which emitted the log</param>
        /// <param name="e">Optional exception to go with the log</param>
        /// <param name="meta">optional dynamic meta object to go with the log</param>
        public UppmLogEntry(string m, object src = null, Exception e = null, dynamic meta = null)
        {
            Source = src;
            Message = m;
            Meta = meta ?? Meta;
            Exception = e ?? Exception;
        }
    }

    /// <summary>
    /// Shortcuts to logging Uppm log entries
    /// </summary>
    public static class LogExtensions
    {
        /// <summary>
        /// Wrapper around log4net Debug, logging as an <see cref="UppmLogEntry"/>
        /// </summary>
        /// <param name="log">log4net log</param>
        /// <param name="message"></param>
        /// <param name="source">optional source object which emitted the log</param>
        /// <param name="meta">optional dynamic meta object to go with the log</param>
        public static void UDebug(this ILog log, string message, object source = null, dynamic meta = null) =>
            log.Debug(new UppmLogEntry(message, source, meta: meta) {Level = Level.Debug});

        /// <summary>
        /// Wrapper around log4net Info, logging as an <see cref="UppmLogEntry"/>
        /// </summary>
        /// <param name="log">log4net log</param>
        /// <param name="message"></param>
        /// <param name="source">optional source object which emitted the log</param>
        /// <param name="meta">optional dynamic meta object to go with the log</param>
        public static void UInfo(this ILog log, string message, object source = null, dynamic meta = null) =>
            log.Info(new UppmLogEntry(message, source, meta: meta));

        /// <summary>
        /// Wrapper around log4net Warn, logging as an <see cref="UppmLogEntry"/>
        /// </summary>
        /// <param name="log">log4net log</param>
        /// <param name="message"></param>
        /// <param name="source">optional source object which emitted the log</param>
        /// <param name="e">Optional exception to go with the log</param>
        /// <param name="meta">optional dynamic meta object to go with the log</param>
        public static void UWarn(this ILog log, string message, object source = null, Exception e = null, dynamic meta = null) =>
            log.Warn(new UppmLogEntry(message, source, e, meta) {Level = Level.Warn}, e);

        /// <summary>
        /// Wrapper around log4net Error, logging as an <see cref="UppmLogEntry"/>
        /// </summary>
        /// <param name="log">log4net log</param>
        /// <param name="message"></param>
        /// <param name="source">optional source object which emitted the log</param>
        /// <param name="e">Optional exception to go with the log</param>
        /// <param name="meta">optional dynamic meta object to go with the log</param>
        public static void UError(this ILog log, string message, object source = null, Exception e = null, dynamic meta = null) =>
            log.Error(new UppmLogEntry(message, source, e, meta) {Level = Level.Error}, e);

        /// <summary>
        /// Wrapper around log4net Fatal, logging as an <see cref="UppmLogEntry"/>
        /// </summary>
        /// <param name="log">log4net log</param>
        /// <param name="message"></param>
        /// <param name="source">optional source object which emitted the log</param>
        /// <param name="e">Optional exception to go with the log</param>
        /// <param name="meta">optional dynamic meta object to go with the log</param>
        public static void UFatal(this ILog log, string message, object source = null, Exception e = null, dynamic meta = null) =>
            log.Fatal(new UppmLogEntry(message, source, e, meta) {Level = Level.Fatal}, e);
    }
}
