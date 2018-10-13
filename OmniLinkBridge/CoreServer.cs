using OmniLinkBridge.Modules;
using OmniLinkBridge.OmniLink;
using log4net;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OmniLinkBridge
{
    public class CoreServer
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OmniLinkII omnilink;
        private readonly List<IModule> modules = new List<IModule>();
        private readonly List<Task> tasks = new List<Task>();

        public CoreServer()
        {
            Thread handler = new Thread(Server);
            handler.Start();
        }

        private void Server()
        {
            Global.running = true;

            log.Debug("Starting up server " +
                Assembly.GetExecutingAssembly().GetName().Version.ToString());

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

            // Startup modules         
            foreach (IModule module in modules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    module.Startup();
                }));
            }

            // Wait for all threads to stop
            Task.WaitAll(tasks.ToArray());
        }

        public void Shutdown()
        {
            Global.running = false;

            // Shutdown modules
            foreach (IModule module in modules)
                module.Shutdown();

            // Wait for all threads to stop
            if (tasks != null)
                Task.WaitAll(tasks.ToArray());

            log.Debug("Shutdown completed");
        }
    }
}