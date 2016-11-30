using System;
using System.Collections.Generic;
using System.Net;

namespace HAILogger
{
    static class WebNotification
    {
        private static List<string> subscriptions = new List<string>();
        private static object subscriptions_lock = new object();

        public static void AddSubscription(string callback)
        {
            lock (subscriptions_lock)
            {
                if (!subscriptions.Contains(callback))
                {
                    Event.WriteVerbose("WebNotification", "Adding subscription to " + callback);
                    subscriptions.Add(callback);
                }
            }
        }

        public static void Send(string type, string body)
        {
            string[] send;
            lock (subscriptions_lock)
                send = subscriptions.ToArray();

            foreach (string subscription in send)
            {
                WebClient client = new WebClient();
                client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                client.Headers.Add("type", type);
                client.UploadStringCompleted += client_UploadStringCompleted;

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

                lock (subscriptions_lock)
                    subscriptions.Remove(e.UserState.ToString());
            }
        }
    }
}