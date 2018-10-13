using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace OmniLinkBridge.WebAPI
{
    static class WebNotification
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static List<string> subscriptions = new List<string>();
        private static object subscriptions_lock = new object();

        public static void AddSubscription(string callback)
        {
            lock (subscriptions_lock)
            {
                if (!subscriptions.Contains(callback))
                {
                    log.Debug("Adding subscription to " + callback);
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
                    log.Error("An error occurred sending notification to " + subscription, ex);
                    subscriptions.Remove(subscription);
                }
            }
        }

        static void client_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                log.Error("An error occurred sending notification to " + e.UserState.ToString(), e.Error);

                lock (subscriptions_lock)
                    subscriptions.Remove(e.UserState.ToString());
            }
        }

        public static void RestoreSubscriptions()
        {
            string json;

            try
            {
                if (File.Exists(Global.webapi_subscriptions_file))
                    json = File.ReadAllText(Global.webapi_subscriptions_file);
                else
                    return;

                lock (subscriptions_lock)
                    subscriptions = JsonConvert.DeserializeObject<List<string>>(json);

                log.Debug("Restored subscriptions from file");
            }
            catch (Exception ex)
            {
                log.Error("An error occurred restoring subscriptions", ex);
            }
        }

        public static void SaveSubscriptions()
        {
            string json;

            lock (subscriptions_lock)
                json = JsonConvert.SerializeObject(subscriptions);

            try
            {
                File.WriteAllText(Global.webapi_subscriptions_file, json);

                log.Debug("Saved subscriptions to file");
            }
            catch (Exception ex)
            {
                log.Error("An error occurred saving subscriptions", ex);
            }
        }
    }
}