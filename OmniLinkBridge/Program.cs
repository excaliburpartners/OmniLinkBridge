using Mono.Unix;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Formatting.Compact;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace OmniLinkBridge
{
    class Program
    {
        static CoreServer server;

        static int Main(string[] args)
        {
            bool interactive = false;

            string config_file = "OmniLinkBridge.ini";
            string log_file = "log.txt";
            bool log_clef = false;
            LogEventLevel log_level = LogEventLevel.Information;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "/?":
                    case "-h":
                    case "-help":
                        ShowHelp();
                        return 0;
                    case "-c":
                        config_file = args[++i];
                        break;
                    case "-e":
                        Global.UseEnvironment = true;
                        break;
                    case "-d":
                        Global.DebugSettings = true;
                        break;
                    case "-lf":
                        log_file = args[++i];

                        if (string.Compare(log_file, "disable", true) == 0)
                            log_file = null;
                        break;
                    case "-lj":
                        log_clef = true;
                        break;
                    case "-ll":
                        Enum.TryParse(args[++i], out log_level);
                        break;
                    case "-s":
                        Global.webapi_subscriptions_file = args[++i];
                        break;
                    case "-i":
                        interactive = true;
                        break;
                }
            }

            config_file = GetFullPath(config_file);

            Global.webapi_subscriptions_file = GetFullPath(Global.webapi_subscriptions_file ?? "WebSubscriptions.json");

            // Use TLS 1.2 as default connection
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            string log_format = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{SourceContext} {Level:u3}] {Message:lj}{NewLine}{Exception}";

            var log_config = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithProperty("Application", "OmniLinkBridge")
                .Enrich.WithProperty("Session", Guid.NewGuid())
                .Enrich.WithProperty("User", (Environment.UserName + Environment.MachineName).GetHashCode())
                .Enrich.FromLogContext();

            if (log_file != null)
            {
                log_file = GetFullPath(log_file);

                if (log_clef)
                    log_config = log_config.WriteTo.Async(a => a.File(new CompactJsonFormatter(), log_file, log_level,
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 15));
                else
                    log_config = log_config.WriteTo.Async(a => a.File(log_file, log_level, log_format, 
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 15));
            }

            if (UseTelemetry())
                log_config = log_config.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(Matching.WithProperty("Telemetry"))
                    .WriteTo.Http("https://telemetry.excalibur-partners.com"));

            if (Environment.UserInteractive || interactive)
                log_config = log_config.WriteTo.Console(outputTemplate: log_format);

            Log.Logger = log_config.CreateLogger();

            try
            {
                Settings.LoadSettings(config_file);
            }
            catch
            {
                // Errors are logged in LoadSettings();
                Log.CloseAndFlush();
                return -1;
            }

            if (Environment.UserInteractive || interactive)
            {
                if (IsRunningOnMono())
                {
                    UnixSignal[] signals = new UnixSignal[]{
                        new UnixSignal(Mono.Unix.Native.Signum.SIGTERM),
                        new UnixSignal(Mono.Unix.Native.Signum.SIGINT),
                        new UnixSignal(Mono.Unix.Native.Signum.SIGUSR1)
                    };

                    Task.Factory.StartNew(() =>
                    {
                        // Blocking call to wait for any kill signal
                        int index = UnixSignal.WaitAny(signals, -1);

                        server.Shutdown();
                    });
                }

                Console.TreatControlCAsInput = false;
                Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);

                server = new CoreServer();
            }
            else
            {
                ServiceBase[] ServicesToRun;

                // More than one user Service may run within the same process. To add
                // another service to this process, change the following line to
                // create a second service object. For example,
                //
                //   ServicesToRun = new ServiceBase[] {new Service1(), new MySecondUserService()};
                //
                ServicesToRun = new ServiceBase[] { new Service() };

                ServiceBase.Run(ServicesToRun);
            }

            return 0;
        }

        static string GetFullPath(string file)
        {
            if (Path.IsPathRooted(file))
                return file;

            return Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), file);
        }

        protected static void myHandler(object sender, ConsoleCancelEventArgs args)
        {
            server.Shutdown();
            args.Cancel = true;
        }

        static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        static bool UseTelemetry()
        {
            return string.Compare(Environment.GetEnvironmentVariable("TELEMETRY_OPTOUT"), "1") != 0;
        }

        static void ShowHelp()
        {
            Console.WriteLine(
                AppDomain.CurrentDomain.FriendlyName + " [-c config_file] [-e] [-d] [-j] [-s subscriptions_file]\n" +
                "\t[-lf log_file|disable] [-lj [-ll verbose|debug|information|warning|error] [-i]\n" +
                "\t-c  Specifies the configuration file. Default is OmniLinkBridge.ini\n" +
                "\t-e  Check environment variables for configuration settings\n" +
                "\t-d  Show debug ouput for configuration loading\n" +
                "\t-s  Specifies the web api subscriptions file. Default is WebSubscriptions.json\n" +
                "\t-lf Specifies the rolling log file. Retention is 15 days. Default is log.txt.\n" +
                "\t-lj Write logs as CLEF (compact log event format) JSON.\n" +
                "\t-ll Minimum level at which events will be logged. Default is information.\n" +
                "\t-i  Run in interactive mode");

            Console.WriteLine(
                "\nOmniLink Bridge collects anonymous telemetry data to help improve the software.\n" +
                "You can opt of telemetry by setting a TELEMETRY_OPTOUT environment variable to 1.");
        }
    }
}
