using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;

namespace OmniLinkBridge
{
    public static class Settings
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void LoadSettings()
        {
            NameValueCollection settings = LoadCollection(Global.config_file);

            // HAI / Leviton Omni Controller
            Global.controller_address = settings["controller_address"];
            Global.controller_port = ValidatePort(settings, "controller_port");
            Global.controller_key1 = settings["controller_key1"];
            Global.controller_key2 = settings["controller_key2"];
            Global.controller_name = settings["controller_name"] ?? "OmniLinkBridge";

            // Controller Time Sync
            Global.time_sync = ValidateYesNo(settings, "time_sync");
            Global.time_interval = ValidateInt(settings, "time_interval");
            Global.time_drift = ValidateInt(settings, "time_drift");

            // Verbose Console
            Global.verbose_unhandled = ValidateYesNo(settings, "verbose_unhandled");
            Global.verbose_event = ValidateYesNo(settings, "verbose_event");
            Global.verbose_area = ValidateYesNo(settings, "verbose_area");
            Global.verbose_zone = ValidateYesNo(settings, "verbose_zone");
            Global.verbose_thermostat_timer = ValidateYesNo(settings, "verbose_thermostat_timer");
            Global.verbose_thermostat = ValidateYesNo(settings, "verbose_thermostat");
            Global.verbose_unit = ValidateYesNo(settings, "verbose_unit");
            Global.verbose_message = ValidateYesNo(settings, "verbose_message");

            // mySQL Logging
            Global.mysql_logging = ValidateYesNo(settings, "mysql_logging");
            Global.mysql_connection = settings["mysql_connection"];

            // Web Service
            Global.webapi_enabled = ValidateYesNo(settings, "webapi_enabled");
            Global.webapi_port = ValidatePort(settings, "webapi_port");
            Global.webapi_override_zone = LoadOverrideZone<WebAPI.OverrideZone>(settings, "webapi_override_zone");

            // MQTT
            Global.mqtt_enabled = ValidateYesNo(settings, "mqtt_enabled");
            Global.mqtt_server = settings["mqtt_server"];
            Global.mqtt_port = ValidatePort(settings, "mqtt_port");
            Global.mqtt_username = settings["mqtt_username"];
            Global.mqtt_password = settings["mqtt_password"];
            Global.mqtt_prefix = settings["mqtt_prefix"] ?? "omnilink";
            Global.mqtt_discovery_prefix = settings["mqtt_discovery_prefix"] ?? "homeassistant";
            Global.mqtt_discovery_name_prefix = settings["mqtt_discovery_name_prefix"] ?? string.Empty;

            if (!string.IsNullOrEmpty(Global.mqtt_discovery_name_prefix))
                Global.mqtt_discovery_name_prefix += " ";

            Global.mqtt_discovery_ignore_zones = ValidateRange(settings, "mqtt_discovery_ignore_zones");
            Global.mqtt_discovery_ignore_units = ValidateRange(settings, "mqtt_discovery_ignore_units");
            Global.mqtt_discovery_override_zone = LoadOverrideZone<MQTT.OverrideZone>(settings, "mqtt_discovery_override_zone");

            // Notifications
            Global.notify_area = ValidateYesNo(settings, "notify_area");
            Global.notify_message = ValidateYesNo(settings, "notify_message");

            // Email Notifications
            Global.mail_server = settings["mail_server"];
            Global.mail_tls = ValidateYesNo(settings, "mail_tls");
            Global.mail_port = ValidatePort(settings, "mail_port");
            Global.mail_username = settings["mail_username"];
            Global.mail_password = settings["mail_password"];
            Global.mail_from = ValidateMailFrom(settings, "mail_from");
            Global.mail_to = ValidateMailTo(settings, "mail_to");

            // Prowl Notifications
            Global.prowl_key = ValidateMultipleStrings(settings, "prowl_key");

            // Pushover Notifications
            Global.pushover_token = settings["pushover_token"];
            Global.pushover_user = ValidateMultipleStrings(settings, "pushover_user");
        }

        private static ConcurrentDictionary<int, T> LoadOverrideZone<T>(NameValueCollection settings, string section) where T : new()
        {
            try
            {
                ConcurrentDictionary<int, T> ret = new ConcurrentDictionary<int, T>();

                if (settings[section] == null)
                    return ret;

                string[] ids = settings[section].Split(',');

                for (int i = 0; i < ids.Length; i++)
                {
                    Dictionary<string, string> attributes = ids[i].TrimEnd(new char[] { ';' }).Split(';')
                        .Select(s => s.Split('='))
                        .ToDictionary(a => a[0].Trim(), a => a[1].Trim(), StringComparer.InvariantCultureIgnoreCase);

                    if (!attributes.ContainsKey("id") || !int.TryParse(attributes["id"], out int attrib_id))
                        throw new Exception("Missing or invalid id attribute");

                    T override_zone = new T();

                    if (((object)override_zone) is WebAPI.OverrideZone webapi_zone)
                    {
                        if (!attributes.ContainsKey("device_type") || !Enum.TryParse(attributes["device_type"], out WebAPI.DeviceType attrib_device_type))
                            throw new Exception("Missing or invalid device_type attribute");

                        webapi_zone.device_type = attrib_device_type;
                    }
                    else if (((object)override_zone) is MQTT.OverrideZone mqtt_zone)
                    {
                        if (!attributes.ContainsKey("device_class") || !Enum.TryParse(attributes["device_class"], out MQTT.BinarySensor.DeviceClass attrib_device_class))
                            throw new Exception("Missing or invalid device_class attribute");

                        mqtt_zone.device_class = attrib_device_class;
                    }

                    ret.TryAdd(attrib_id, override_zone);
                }

                return ret;
            }
            catch (Exception ex)
            {
                log.Error("Invalid override zone specified for " + section, ex);
                throw;
            }
        }

        private static int ValidateInt(NameValueCollection settings, string section)
        {
            try
            {
                return int.Parse(settings[section]);
            }
            catch
            {
                log.Error("Invalid integer specified for " + section);
                throw;
            }
        }

        private static HashSet<int> ValidateRange(NameValueCollection settings, string section)
        {
            try
            {
                return new HashSet<int>(settings[section].ParseRanges());
            }
            catch
            {
                log.Error("Invalid range specified for " + section);
                throw;
            }
        }

        private static int ValidatePort(NameValueCollection settings, string section)
        {
            try
            {
                int port = int.Parse(settings[section]);

                if (port < 1 || port > 65534)
                    throw new Exception();

                return port;
            }
            catch
            {
                log.Error("Invalid port specified for " + section);
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
                log.Error("Invalid email specified for " + section);
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
                log.Error("Invalid email specified for " + section);
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
                log.Error("Invalid string specified for " + section);
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
                log.Error("Invalid yes/no specified for " + section);
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
            catch (FileNotFoundException ex)
            {
                log.Error("Unable to parse settings file " + sFile, ex);
                throw;
            }

            return settings;
        }
    }
}
