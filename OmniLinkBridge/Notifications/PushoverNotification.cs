using log4net;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Reflection;

namespace OmniLinkBridge.Notifications
{
    public class PushoverNotification : INotification
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Uri URI = new Uri("https://api.pushover.net/1/messages.json");

        public void Notify(string source, string description, NotificationPriority priority)
        {
            foreach (string key in Global.pushover_user)
            {
                var parameters = new NameValueCollection {
                    { "token", Global.pushover_token },
                    { "user", key },
                    { "priority", ((int)priority).ToString() },
                    { "title", source },
                    { "message", description }
                };

                using (WebClient client = new WebClient())
                {
                    client.UploadValues(URI, parameters);
                    client.UploadStringCompleted += client_UploadStringCompleted;
                }
            }
        }

        private void client_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e)
        {
            if (e.Error != null)
                log.Error("An error occurred sending notification", e.Error);
        }
    }
}
