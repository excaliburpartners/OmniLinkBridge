using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace OmniLinkBridge.Notifications
{
    public class ProwlNotification : INotification
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Uri URI = new Uri("https://api.prowlapp.com/publicapi/add");

        public void Notify(string source, string description, NotificationPriority priority)
        {
            foreach (string key in Global.prowl_key)
            {
                List<string> parameters = new List<string>();

                parameters.Add("apikey=" + key);
                parameters.Add("priority= " + (int)priority);
                parameters.Add("application=OmniLinkBridge");
                parameters.Add("event=" + source);
                parameters.Add("description=" + description);

                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    client.UploadStringAsync(URI, string.Join("&", parameters.ToArray()));
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
