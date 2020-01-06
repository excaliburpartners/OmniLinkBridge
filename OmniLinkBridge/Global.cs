using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Mail;
using System.Reflection;

namespace OmniLinkBridge
{
    public abstract class Global
    {
        public static bool DebugSettings { get; set; }
        public static bool UseEnvironment { get; set; }

        // HAI / Leviton Omni Controller
        public static string controller_address;
        public static int controller_port;
        public static string controller_key1;
        public static string controller_key2;
        public static string controller_name;

        // Time Sync
        public static bool time_sync;
        public static int time_interval;
        public static int time_drift;

        // Verbose Console
        public static bool verbose_unhandled;
        public static bool verbose_event;
        public static bool verbose_area;
        public static bool verbose_zone;
        public static bool verbose_thermostat_timer;
        public static bool verbose_thermostat;
        public static bool verbose_unit;
        public static bool verbose_message;

        // mySQL Logging
        public static bool mysql_logging;
        public static string mysql_connection;

        // Web Service
        public static bool webapi_enabled;
        public static int webapi_port;
        public static ConcurrentDictionary<int, WebAPI.OverrideZone> webapi_override_zone;
        public static string webapi_subscriptions_file;

        // MQTT
        public static bool mqtt_enabled;
        public static string mqtt_server;
        public static int mqtt_port;
        public static string mqtt_username;
        public static string mqtt_password;
        public static string mqtt_prefix;
        public static string mqtt_discovery_prefix;
        public static string mqtt_discovery_name_prefix;
        public static HashSet<int> mqtt_discovery_ignore_zones;
        public static HashSet<int> mqtt_discovery_ignore_units;
        public static ConcurrentDictionary<int, MQTT.OverrideZone> mqtt_discovery_override_zone;

        // Notifications
        public static bool notify_area;
        public static bool notify_message;

        // Email Notifications
        public static string mail_server;
        public static bool mail_tls;
        public static int mail_port;
        public static string mail_username;
        public static string mail_password;
        public static MailAddress mail_from;
        public static MailAddress[] mail_to;

        // Prowl Notifications
        public static string[] prowl_key;

        // Pushover Notifications
        public static string pushover_token;
        public static string[] pushover_user;

        public static object GetValue(string propName)
        {
            return typeof(Global).GetField(propName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }
    }
}
