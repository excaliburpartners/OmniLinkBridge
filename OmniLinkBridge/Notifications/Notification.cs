using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace OmniLinkBridge.Notifications
{
    public static class Notification
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly List<INotification> providers = new List<INotification>()
        {
            new EmailNotification(),
            new ProwlNotification(),
            new PushoverNotification()
        };

        public static void Notify(string source, string description, NotificationPriority priority = NotificationPriority.Normal)
        {
            Parallel.ForEach(providers, (provider) =>
            {
                try
                {
                    provider.Notify(source, description, priority);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to send notification");
                }
            });
        }
    }
}
