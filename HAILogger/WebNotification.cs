using System;
using System.Collections.Generic;
using System.Net;

namespace HAILogger
{
    static class WebNotification
    {
        private static List<string> subscriptions = new List<string>();

        public static void AddSubscription(string callback)
        {
            if (!subscriptions.Contains(callback))
            {
                Event.WriteVerbose("WebRequest", "Adding subscription to " + callback);
                subscriptions.Add(callback);
            }
        }

        public static void Send(string type, string body)
        {
            WebClient client = new WebClient();
            client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            client.Headers.Add("type", type);
            client.UploadStringCompleted += client_UploadStringCompleted;

            foreach (string subscription in subscriptions)
            {
                try
                {
                    client.UploadStringAsync(new Uri(subscription), "POST", body, subscription);
                }
                catch (Exception ex)
                {
                    Event.WriteError("WebNotification", "An error occurred sending notification to " + subscription + "\r\n" + ex.ToString());
                    subscriptions.Remove(subscription);
                }
            }
        }

        static void client_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Event.WriteError("WebNotification", "An error occurred sending notification to " + e.UserState.ToString() + "\r\n" + e.Error.Message);
                subscriptions.Remove(e.UserState.ToString());
            }
        }
    }
}
