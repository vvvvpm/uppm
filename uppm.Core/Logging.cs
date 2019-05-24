using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;
using md.stdl.String;
using Serilog;
using Serilog.Core;
using Serilog.Enricher;
using Serilog.Events;

namespace uppm.Core
{
    /// <summary>
    /// Inheriting class can easily provide its own context while logging
    /// </summary>
    public interface ILogging
    {
        /// <summary>
        /// The logger providing the context to the inheriting class
        /// </summary>
        /// <remarks>
        /// It is recommended to assign this autoproperty <see cref="Logging.GetContext"/> in the constructor.
        /// </remarks>
        ILogger Log { get; }

        /// <summary>
        /// Event which can produce rapidly changing progress information
        /// which would potentially pollute or put unnecessary stress onto Serilog loggers.
        /// </summary>
        event UppmProgressHandler OnProgress;

        /// <summary>
        /// Invoke any progress change. This is only meant to be used internally by uppm
        /// via the extension method <see cref="Logging.InvokeAnyProgress"/>
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

    /// <summary>
    /// Event arguments for user input queries
    /// </summary>
    public class AskUserEventArgs
    {
        /// <summary></summary>
        public string Question { get; internal set; }

        /// <summary>
        /// Desired default value used in a potential unattended mode
        /// </summary>
        public string Default { get; internal set; }

        /// <summary>
        /// Optional possibilities. If null anything is allowed.
        /// </summary>
        public IEnumerable<string> Possibilities { get; internal set; }

        /// <summary></summary>
        public string Answer { internal get; set; }
    }

    /// <summary>
    /// A global event which can produce rapidly changing progress information
    /// which would potentially pollute or put unnecessary stress onto Serilog loggers.
    /// </summary>
    /// <param name="sender">The source of the progress</param>
    /// <param name="args"></param>
    public delegate void UppmProgressHandler(ILogging sender, ProgressEventArgs args);

    /// <summary>
    /// If uppm needs user input this event is fired. The subscriber should assign the <see cref="AskUserEventArgs.Answer"/> in the args.
    /// If <see cref="AskUserEventArgs.Answer"/> is not assigned or null or empty the default is assumed.
    /// </summary>
    /// <param name="sender">possible sender logger. It can be null though</param>
    /// <param name="args"></param>
    public delegate void UppmAskUserHandler(ILogging sender, AskUserEventArgs args);

    /// <summary>
    /// Utilities for logging
    /// </summary>
    public static class Logging
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
        /// Shortcut to getting a consistent "UppmSource" property when <see cref="ILogging"/> objects log.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static ILogger GetContext(this ILogging source)
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
        public static void InvokeAnyProgress(this ILogging source, double totalValue = 0, double currentValue = 0, string state = "", string message = "")
        {
            var prog = new ProgressEventArgs(totalValue, currentValue, state, message);
            source?.InvokeProgress(prog);
            OnAnyProgress?.Invoke(source, prog);
        }

        /// <summary>
        /// A global event which can produce rapidly changing progress information
        /// which would potentially pollute or put unnecessary stress onto Serilog loggers.
        /// </summary>
        public static event UppmProgressHandler OnAnyProgress;

        /// <summary>
        /// When user input is needed anywhere during using uppm this method should be called.
        /// It invokes <see cref="OnQuestion"/> where the implementer can either block uppm with user query or use the default value.
        /// </summary>
        /// <param name="question">Question to be asked from user</param>
        /// <param name="possibilities">If null any input will be accepted. Otherwise input is compared to these possible entries ignoring case. If no match is found question will be asked again.</param>
        /// <param name="defaultValue">This value is used when user submits an empty input or in a potential unattended mode.</param>
        /// <param name="source">Optional logger which asks the question</param>
        /// <returns>User answer or default</returns>
        public static string AskUser(
            string question,
            IEnumerable<string> possibilities = null,
            string defaultValue = "",
            ILogging source = null)
        {
            var possarray = possibilities?.ToArray();

            L.Information(question);
            L.Information("    ({Possibilities})", possarray.Humanize("or"));
            L.Information("    (default is {DefaultValue})", defaultValue);

            var args = new AskUserEventArgs
            {
                Question = question,
                Default = defaultValue
            };
            OnQuestion?.Invoke(source, args);
            if (possarray != null &&
                !possarray.Any(p => p.EqualsCaseless(args.Answer)))
            {
                L.Warning("Invalid answer submitted for question: {Answer}\n    Asking again.", args.Answer);
                AskUser(question, possarray, defaultValue, source);
            }

            var res = string.IsNullOrWhiteSpace(args.Answer) ? defaultValue : args.Answer;
            L.Debug("{Answer} is answered", res);
            return res;
        }

        /// <summary>
        /// A shortcut to <see cref="AskUser"/> for selecting an enumeration
        /// </summary>
        /// <typeparam name="T">Enumeration type</typeparam>
        /// <param name="question">Question to be asked from user</param>
        /// <param name="possibilities">If null any input will be accepted. Otherwise input is compared to these possible entries ignoring case. If no match is found question will be asked again.</param>
        /// <param name="defaultValue">This value is used when user submits an empty input or in a potential unattended mode.</param>
        /// <param name="source">Optional logger which asks the question</param>
        /// <returns></returns>
        public static T AskUserEnum<T>(
            string question,
            IEnumerable<T> possibilities = null,
            T defaultValue = default(T),
            ILogging source = null) where T : struct
        {
            if (typeof(T).IsEnum) throw new ArgumentException($"{typeof(T)} type is not enum.");
            var poss = possibilities == null ? Enum.GetNames(typeof(T)) : possibilities.Select(p => p.ToString());
            var resstr = AskUser(question, poss, defaultValue.ToString(), source);
            return Enum.TryParse<T>(resstr, true, out var res) ? res : defaultValue;
        }

        /// <summary>
        /// A shortcut to <see cref="AskUser"/> for yes (true) / no (false) questions
        /// </summary>
        /// <param name="question">Question to be asked from user</param>
        /// <param name="defaultValue">This value is used when user submits an empty input or in a potential unattended mode.</param>
        /// <param name="source">Optional logger which asks the question</param>
        /// <returns>User answer or default</returns>
        public static bool ConfirmWithUser(
            string question,
            bool defaultValue = true,
            ILogging source = null)
        {
            var res = AskUser(
                question,
                new[] {"Y", "N"},
                defaultValue ? "Y" : "N",
                source);
            if (res.EqualsCaseless("Y")) return true;
            if (res.EqualsCaseless("N")) return false;
            return defaultValue;
        }

        /// <summary>
        /// If uppm needs user input this event is fired. The subscriber should assign the <see cref="AskUserEventArgs.Answer"/> in the args.
        /// If <see cref="AskUserEventArgs.Answer"/> is not assigned or null or empty the default is assumed.
        /// </summary>
        public static event UppmAskUserHandler OnQuestion;

        /// <summary>
        /// Not implemented functionality notification convenience method.
        /// </summary>
        public static void NotYetImplemented()
        {
            var stacktrace = new StackTrace();
            L.Error("Invoked functionality is not implemented at:\n{$StackTrace}", stacktrace);
        }
    }
}
