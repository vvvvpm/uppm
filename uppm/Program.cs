using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerArgs;
using Serilog;
using Serilog.Events;
using uppm.Core;

namespace uppm
{
    public class Program
    {
        private static UppmArguments _arguments;

        private static void BreakOnError(LogEvent le)
        {
            if ((int)le.Level >= (int)LogEventLevel.Error && !(_arguments.Unattended || _arguments.ContinueOnError))
            {
                var resp = Console.ReadKey(true);
                if (resp.Key == ConsoleKey.Escape) Environment.Exit(-1);
            }
        }

        public static void InitUppm()
        {
            Logging.OnQuestion += HandleOnQuestion;
            //TODO: Handle progress

            Logging.ConfigureLogger(
                logger => logger
                    .MinimumLevel.Is(_arguments.LoggingLevel)
                    .WriteTo.Console(),
                obslog => obslog.Subscribe(BreakOnError)
            );

            TargetApp.RegisterApps(typeof(TargetApp).Assembly);
            TargetApp.RegisterApps(typeof(Program).Assembly);

            foreach (var knownApp in TargetApp.KnownTargetApps.Values)
            {
                if (Enum.TryParse<Architecture>(_arguments.AppArchitecture, true, out var arch))
                {
                    knownApp.DefaultArchitecture = arch;
                }
            }

            if (!string.IsNullOrWhiteSpace(_arguments.WorkDirOverride))
                Uppm.WorkingDirectory = _arguments.WorkDirOverride;

            // TODO: further uppm initialization
        }

        private static void HandleOnQuestion(ILogging sender, AskUserEventArgs args)
        {
            args.Answer = !_arguments.Unattended ? Console.ReadLine() : args.Default;
        }

        private static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine($"uppm {Uppm.CoreVersion} by MESO Digital Interiors GmbH.");

            //TODO: register uppm uri scheme

            _arguments = Args.Parse<UppmArguments>(args);
            if (!_arguments.Unattended)
            {
                _arguments.Quiet = false;
            }

            InitUppm();

            // Legal-proofing
            if(_arguments.Unattended)
                Logging.L.Information(
                    "Uppm runs in Unattended mode. " +
                    "All licenses are agreed upon automatically and it's expected that " +
                    "the user is fully aware of all terms regarding any potential pack uppm might install."
                );
            
            // TODO: interpret uppm-ref: url
            // TODO: execute pack action
        }
    }
}
