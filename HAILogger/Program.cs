using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace HAILogger
{
    class Program
    {
        static CoreServer server;

        static void Main(string[] args)
        {
            bool interactive = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "/?":
                    case "-h":
                    case "-help":
                        ShowHelp();
                        return;
                    case "-c":
                        Global.config_file = args[++i];
                        break;
                    case "-l":
                        Global.config_file = args[++i];
                        break;
                    case "-i":
                        interactive = true;
                        break;
                }
            }

            if (string.IsNullOrEmpty(Global.log_file))
                Global.log_file = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) +
                    Path.DirectorySeparatorChar + "EventLog.txt";

            if (string.IsNullOrEmpty(Global.config_file))
                Global.config_file = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) +
                    Path.DirectorySeparatorChar + "HAILogger.ini";

            Global.event_source = "HAI Logger";

            try
            {
                Settings.LoadSettings();
            }
            catch
            {
                // Errors are logged in LoadSettings();
                Environment.Exit(1);
            }

            if (Environment.UserInteractive || interactive)
            {
                Console.TreatControlCAsInput = false;
                Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);

                Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

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
        }

        protected static void myHandler(object sender, ConsoleCancelEventArgs args)
        {
            server.Shutdown();
            args.Cancel = true;
        }

        static void ShowHelp()
        {
            Console.WriteLine(
                AppDomain.CurrentDomain.FriendlyName + " [-c config_file] [-l log_file] [-i]\n" +
                "\t-c Specifies the name of the config file. Default is HAILogger.ini\n" +
                "\t-l Specifies the name of the log file. Default is EventLog.txt\n" +
                "\t-i Run in interactive mode");
        }
    }
}
