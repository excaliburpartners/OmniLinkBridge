using System.Net.Mail;

namespace HAILogger
{
    public abstract class Global
    {
        // Events
        public static string event_source;

        // Files
        public static string config_file;
        public static string log_file;

        // HAI Controller
        public static string hai_address;
        public static int hai_port;
        public static string hai_key1;
        public static string hai_key2;
        public static bool hai_time_sync;
        public static int hai_time_interval;
        public static int hai_time_drift;

        // mySQL Database
        public static bool mysql_logging;
        public static string mysql_connection;

        // Events
        public static string mail_server;
        public static int mail_port;
        public static string mail_username;
        public static string mail_password;
        public static MailAddress mail_from;
        public static MailAddress[] mail_to;
        public static MailAddress[] mail_alarm_to;

        // Prowl Notifications
        public static string[] prowl_key;
        public static bool prowl_messages;

        // Web Service
        public static bool webapi_enabled;
        public static int webapi_port;

        // Verbose Output
        public static bool verbose_unhandled;
        public static bool verbose_event;
        public static bool verbose_area;
        public static bool verbose_zone;
        public static bool verbose_thermostat_timer;
        public static bool verbose_thermostat;
        public static bool verbose_unit;
        public static bool verbose_message;
    }
}
