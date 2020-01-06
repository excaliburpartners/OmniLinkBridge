using OmniLinkBridge.Modules;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OmniLinkBridge
{
    public class CoreServer
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);
        
        private OmniLinkII omnilink;
        private readonly List<IModule> modules = new List<IModule>();
        private readonly List<Task> tasks = new List<Task>();
        private readonly ManualResetEvent quitEvent = new ManualResetEvent(false);
        private DateTime startTime;

        public CoreServer()
        {
            Thread handler = new Thread(Server);
            handler.Start();
        }

        private void Server()
        {
            // Controller connection
            modules.Add(omnilink = new OmniLinkII(Global.controller_address, Global.controller_port, Global.controller_key1, Global.controller_key2));

            // Initialize modules
            modules.Add(new LoggerModule(omnilink));

            if (Global.time_sync)
                modules.Add(new TimeSyncModule(omnilink));

            if (Global.webapi_enabled)
                modules.Add(new WebServiceModule(omnilink));

            if(Global.mqtt_enabled)
                modules.Add(new MQTTModule(omnilink));

            startTime = DateTime.Now;

            using (LogContext.PushProperty("Telemetry", "Startup"))
                log.Information("Started version {Version} on {OperatingSystem} with {Modules}",
                    Assembly.GetExecutingAssembly().GetName().Version, Environment.OSVersion, modules);

            // Startup modules         
            foreach (IModule module in modules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    module.Startup();
                }));
            }

            quitEvent.WaitOne();
        }

        public void Shutdown()
        {
            // Shutdown modules
            foreach (IModule module in modules)
                module.Shutdown();

            // Wait for all threads to stop
            if (tasks != null)
                Task.WaitAll(tasks.ToArray());

            using (LogContext.PushProperty("Telemetry", "Shutdown"))
                log.Information("Shutdown completed with uptime {Uptime}", (DateTime.Now - startTime).ToString());

            Log.CloseAndFlush();

            quitEvent.Set();
        }
    }
}