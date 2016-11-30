using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Mail;

namespace HAILogger
{
    static class Settings
    {
        public static void LoadSettings()
        {
            NameValueCollection settings = LoadCollection(Global.config_file);

            // HAI Controller
            Global.hai_address = settings["hai_address"];
            Global.hai_port = ValidatePort(settings, "hai_port");
            Global.hai_key1 = settings["hai_key1"];
            Global.hai_key2 = settings["hai_key2"];
            Global.hai_time_sync = ValidateYesNo(settings, "hai_time_sync");
            Global.hai_time_interval = ValidateInt(settings, "hai_time_interval");
            Global.hai_time_drift = ValidateInt(settings, "hai_time_drift");

            // mySQL Database
            Global.mysql_logging = ValidateYesNo(settings, "mysql_logging");
            Global.mysql_connection = settings["mysql_connection"];

            // Events
            Global.mail_server = settings["mail_server"];
            Global.mail_port = ValidatePort(settings, "mail_port");
            Global.mail_username = settings["mail_username"];
            Global.mail_password = settings["mail_password"];
            Global.mail_from = ValidateMailFrom(settings, "mail_from");
            Global.mail_to = ValidateMailTo(settings, "mail_to");
            Global.mail_alarm_to = ValidateMailTo(settings, "mail_alarm_to");

            // Prowl Notifications
            Global.prowl_key = ValidateMultipleStrings(settings, "prowl_key");
            Global.prowl_messages = ValidateYesNo(settings, "prowl_messages");

            // Web Service
            Global.webapi_enabled = ValidateYesNo(settings, "webapi_enabled");
            Global.webapi_port = ValidatePort(settings, "webapi_port");

            // Verbose Output
            Global.verbose_unhandled = ValidateYesNo(settings, "verbose_unhandled");
            Global.verbose_event = ValidateYesNo(settings, "verbose_event");
            Global.verbose_area = ValidateYesNo(settings, "verbose_area");
            Global.verbose_zone = ValidateYesNo(settings, "verbose_zone");
            Global.verbose_thermostat_timer = ValidateYesNo(settings, "verbose_thermostat_timer");
            Global.verbose_thermostat = ValidateYesNo(settings, "verbose_thermostat");
            Global.verbose_unit = ValidateYesNo(settings, "verbose_unit");
            Global.verbose_message = ValidateYesNo(settings, "verbose_message");
        }

        private static int ValidateInt(NameValueCollection settings, string section)
        {
            try
            {
                return Int32.Parse(settings[section]);
            }
            catch
            {
                Event.WriteError("Settings", "Invalid integer specified for " + section);
                throw;
            }
        }

        private static int ValidatePort(NameValueCollection settings, string section)
        {
            try
            {
                int port = Int32.Parse(settings[section]);

                if (port < 1 || port > 65534)
                    throw new Exception();

                return port;
            }
            catch
            {
                Event.WriteError("Settings", "Invalid port specified for " + section);
                throw;
            }
        }

        private static bool ValidateBool(NameValueCollection settings, string section)
        {
            try
            {
                return Boolean.Parse(settings[section]);
            }
            catch
            {
                Event.WriteError("Settings", "Invalid bool specified for " + section);
                throw;
            }
        }

        private static IPAddress ValidateIP(NameValueCollection settings, string section)
        {
            if (settings[section] == "*")
                return IPAddress.Any;

            if (settings[section] == "")
                return IPAddress.None;

            try
            {
                return IPAddress.Parse(section);
            }
            catch
            {
                Event.WriteError("Settings", "Invalid IP specified for " + section);
                throw;
            }
        }

        private static string ValidateDirectory(NameValueCollection settings, string section)
        {
            try
            {
                if (!Directory.Exists(settings[section]))
                    Directory.CreateDirectory(settings[section]);

                return settings[section];
            }
            catch
            {
                Event.WriteError("Settings", "Invalid directory specified for " + section);
                throw;
            }
        }

        private static MailAddress ValidateMailFrom(NameValueCollection settings, string section)
        {
            try
            {
                return new MailAddress(settings[section]);
            }
            catch
            {
                Event.WriteError("Settings", "Invalid email specified for " + section);
                throw;
            }
        }

        private static MailAddress[] ValidateMailTo(NameValueCollection settings, string section)
        {
            try
            {
                if(settings[section] == null)
                    return new MailAddress[] {};

                string[] emails = settings[section].Split(',');
                MailAddress[] addresses = new MailAddress[emails.Length];

                for(int i=0; i < emails.Length; i++)
                    addresses[i] = new MailAddress(emails[i]);

                return addresses;
            }
            catch
            {
                Event.WriteError("Settings", "Invalid email specified for " + section);
                throw;
            }
        }

        private static string[] ValidateMultipleStrings(NameValueCollection settings, string section)
        {
            try
            {
                if (settings[section] == null)
                    return new string[] { };

                return settings[section].Split(',');
            }
            catch
            {
                Event.WriteError("Settings", "Invalid string specified for " + section);
                throw;
            }
        }

        private static bool ValidateYesNo (NameValueCollection settings, string section)
        {
            if (settings[section] == null)
                return false;
            if (string.Compare(settings[section], "yes", true) == 0)
                return true;
            else if (string.Compare(settings[section], "no", true) == 0)
                return false;
            else
            {
                Event.WriteError("Settings", "Invalid yes/no specified for " + section);
                throw new Exception();
            }
        }

        private static NameValueCollection LoadCollection(string sFile)
        {
            NameValueCollection settings = new NameValueCollection();

            try
            {
                FileStream fs = new FileStream(sFile, FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(fs);

                while (true)
                {
                    string line = sr.ReadLine();

                    if (line == null)
                        break;

                    if (line.StartsWith("#"))
                        continue;

                    int pos = line.IndexOf('=', 0);

                    if (pos == -1)
                        continue;

                    string key = line.Substring(0, pos).Trim();
                    string value = line.Substring(pos + 1).Trim();

                    settings.Add(key, value);
                }

                sr.Close();
                fs.Close();
            }
            catch (FileNotFoundException)
            {
                Event.WriteError("Settings", "Unable to parse settings file " + sFile);
                throw;
            }

            return settings;
        }
    }
}
