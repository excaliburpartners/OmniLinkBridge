using System;
using System.Collections.Generic;
using System.Net;

namespace HAILogger
{
    public enum ProwlPriority
    {
        VeryLow = -2,
        Moderate = -1,
        Normal = 0,
        High = 1,
        Emergency = 2,
    };

    static class Prowl
    {
        public static void Notify(string source, string description)
        {
            Notify(source, description, ProwlPriority.Normal);
        }

        public static void Notify(string source, string description, ProwlPriority priority)
        {
            Uri URI = new Uri("https://api.prowlapp.com/publicapi/add");

            foreach (string key in Global.prowl_key)
            {
                List<string> parameters = new List<string>();

                parameters.Add("apikey=" + key);
                parameters.Add("priority= " + (int)priority);
                parameters.Add("application=" + Global.event_source);
                parameters.Add("event=" + source);
                parameters.Add("description=" + description);

                WebClient client = new WebClient();
                client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                client.UploadStringAsync(URI, string.Join("&", parameters.ToArray()));
                client.UploadStringCompleted += client_UploadStringCompleted;
            }
        }

        static void client_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e)
        {
            if(e.Error != null)
                Event.WriteError("ProwlNotification", "An error occurred sending notification\r\n" + e.Error.Message);
        }
    }
}
