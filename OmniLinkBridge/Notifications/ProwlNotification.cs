using Serilog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace OmniLinkBridge.Notifications
{
    public class ProwlNotification : INotification
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Uri URI = new Uri("https://api.prowlapp.com/publicapi/add");

        public void Notify(string source, string description, NotificationPriority priority)
        {
            foreach (string key in Global.prowl_key)
            {
                List<string> parameters = new List<string>
                {
                    "apikey=" + key,
                    "priority= " + (int)priority,
                    "application=" + Global.controller_name,
                    "event=" + source,
                    "description=" + description
                };

                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    client.UploadStringAsync(URI, string.Join("&", parameters.ToArray()));
                    client.UploadStringCompleted += Client_UploadStringCompleted;
                }
            }
        }

        private void Client_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e)
        {
            if (e.Error != null)
                log.Error(e.Error, "An error occurred sending prowl notification");
        }
    }
}
