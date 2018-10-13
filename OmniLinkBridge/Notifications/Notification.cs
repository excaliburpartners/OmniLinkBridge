using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmniLinkBridge.Notifications
{
    public static class Notification
    {
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
                provider.Notify(source, description, priority);
            });
        }
    }
}
