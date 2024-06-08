using OmniLinkBridge.MQTT.HomeAssistant;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using ha = OmniLinkBridge.MQTT.HomeAssistant;

namespace OmniLinkBridge
{
    public static class Settings
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        public static void LoadSettings(string file)
        {
            LoadSettings(LoadCollection(file));
        }

        public static void LoadSettings(string[] lines)
        {
            LoadSettings(LoadCollection(lines));
        }

        public static void LoadSettings(NameValueCollection settings)
        {
            // HAI / Leviton Omni Controller
            Global.controller_address = settings.ValidateHasValue("controller_address");
            Global.controller_port = settings.ValidatePort("controller_port");
            Global.controller_key1 = settings.ValidateEncryptionKey("controller_key1");
            Global.controller_key2 = settings.ValidateEncryptionKey("controller_key2");
            Global.controller_name = settings.CheckEnv("controller_name") ?? "OmniLinkBridge";
            Global.controller_id = (Global.controller_address + Global.controller_key1 + Global.controller_key2).ComputeGuid();

            // Controller Time Sync
            Global.time_sync = settings.ValidateBool("time_sync");

            if (Global.time_sync)
            {
                Global.time_interval = settings.ValidateInt("time_interval");
                Global.time_drift = settings.ValidateInt("time_drift");
            }

            // Verbose Console
            Global.verbose_unhandled = settings.ValidateBool("verbose_unhandled");
            Global.verbose_event = settings.ValidateBool("verbose_event");
            Global.verbose_area = settings.ValidateBool("verbose_area");
            Global.verbose_zone = settings.ValidateBool("verbose_zone");
            Global.verbose_thermostat_timer = settings.ValidateBool("verbose_thermostat_timer");
            Global.verbose_thermostat = settings.ValidateBool("verbose_thermostat");
            Global.verbose_unit = settings.ValidateBool("verbose_unit");
            Global.verbose_message = settings.ValidateBool("verbose_message");
            Global.verbose_lock = settings.ValidateBool("verbose_lock");
            Global.verbose_audio = settings.ValidateBool("verbose_audio");

            // mySQL Logging
            Global.mysql_logging = settings.ValidateBool("mysql_logging");
            Global.mysql_connection = settings.CheckEnv("mysql_connection", true);

            // Web Service
            Global.webapi_enabled = settings.ValidateBool("webapi_enabled");

            if (Global.webapi_enabled)
            {
                Global.webapi_port = settings.ValidatePort("webapi_port");
                Global.webapi_override_zone = settings.LoadOverrideZone<WebAPI.OverrideZone>("webapi_override_zone");
            }

            // MQTT
            Global.mqtt_enabled = settings.ValidateBool("mqtt_enabled");

            if (Global.mqtt_enabled)
            {
                Global.mqtt_server = settings.CheckEnv("mqtt_server");
                Global.mqtt_port = settings.ValidatePort("mqtt_port");
                Global.mqtt_username = settings.CheckEnv("mqtt_username", true);
                Global.mqtt_password = settings.CheckEnv("mqtt_password", true);
                Global.mqtt_prefix = settings.CheckEnv("mqtt_prefix") ?? "omnilink";
                Global.mqtt_discovery_prefix = settings.CheckEnv("mqtt_discovery_prefix") ?? "homeassistant";
                Global.mqtt_discovery_name_prefix = settings.CheckEnv("mqtt_discovery_name_prefix") ?? string.Empty;

                if (!string.IsNullOrEmpty(Global.mqtt_discovery_name_prefix))
                    Global.mqtt_discovery_name_prefix += " ";

                Global.mqtt_discovery_ignore_zones = settings.ValidateRange("mqtt_discovery_ignore_zones");
                Global.mqtt_discovery_ignore_units = settings.ValidateRange("mqtt_discovery_ignore_units");
                Global.mqtt_discovery_override_area = settings.LoadOverrideArea<MQTT.OverrideArea>("mqtt_discovery_override_area");
                Global.mqtt_discovery_override_zone = settings.LoadOverrideZone<MQTT.OverrideZone>("mqtt_discovery_override_zone");
                Global.mqtt_discovery_override_unit = settings.LoadOverrideUnit<MQTT.OverrideUnit>("mqtt_discovery_override_unit");
                Global.mqtt_discovery_button_type = settings.ValidateType("mqtt_discovery_button_type", typeof(Switch), typeof(Button));
                Global.mqtt_audio_local_mute = settings.ValidateBool("mqtt_audio_local_mute");
                Global.mqtt_audio_volume_media_player = settings.ValidateBool("mqtt_audio_volume_media_player");
            }

            // Notifications
            Global.notify_area = settings.ValidateBool("notify_area");
            Global.notify_message = settings.ValidateBool("notify_message");

            // Email Notifications
            Global.mail_server = settings.CheckEnv("mail_server");

            if (!string.IsNullOrEmpty(Global.mail_server))
            {
                Global.mail_tls = settings.ValidateBool("mail_tls");
                Global.mail_port = settings.ValidatePort("mail_port");
                Global.mail_username = settings.CheckEnv("mail_username", true);
                Global.mail_password = settings.CheckEnv("mail_password", true);
                Global.mail_from = settings.ValidateMailFrom("mail_from");
                Global.mail_to = settings.ValidateMailTo("mail_to");
            }

            // Prowl Notifications
            Global.prowl_key = settings.ValidateMultipleStrings("prowl_key", true);

            // Pushover Notifications
            Global.pushover_token = settings.CheckEnv("pushover_token", true);
            Global.pushover_user = settings.ValidateMultipleStrings("pushover_user", true);
        }

        private static string CheckEnv(this NameValueCollection settings, string name, bool sensitive = false)
        {
            string env = Global.UseEnvironment ? Environment.GetEnvironmentVariable(name.ToUpper()) : null;
            string value = !string.IsNullOrEmpty(env) ? env : settings[name];

            if (Global.DebugSettings)
                log.Debug("{ConfigType} {ConfigName}: {ConfigValue}", 
                    (!string.IsNullOrEmpty(env) ? "ENV" : "CONF").PadRight(4), name, 
                    sensitive && value != null ? value.Truncate(3) + "***MASKED***" : value);

            return value;
        }

        private static ConcurrentDictionary<int, T> LoadOverrideArea<T>(this NameValueCollection settings, string section) where T : new()
        {
            try
            {
                ConcurrentDictionary<int, T> ret = new ConcurrentDictionary<int, T>();

                string value = settings.CheckEnv(section);

                if (string.IsNullOrEmpty(value))
                    return ret;

                string[] ids = value.Split(',');

                for (int i = 0; i < ids.Length; i++)
                {
                    Dictionary<string, string> attributes = ids[i].TrimEnd(new char[] { ';' }).Split(';')
                        .Select(s => s.Split('='))
                        .ToDictionary(a => a[0].Trim(), a => a[1].Trim(), StringComparer.InvariantCultureIgnoreCase);

                    if (!attributes.ContainsKey("id") || !int.TryParse(attributes["id"], out int attrib_id))
                        throw new Exception("Missing or invalid id attribute");

                    T override_area = new T();

                    if (override_area is MQTT.OverrideArea mqtt_area)
                    {
                        foreach (string attribute in attributes.Keys)
                        {
                            switch(attribute)
                            {
                                case "id":
                                    continue;
                                case "code_arm":
                                    if (!bool.TryParse(attributes["code_arm"], out bool code_arm))
                                        throw new Exception("Invalid code_arm attribute");
                                    mqtt_area.code_arm = code_arm;
                                    break;
                                case "code_disarm":
                                    if (!bool.TryParse(attributes["code_disarm"], out bool code_disarm))
                                        throw new Exception("Invalid code_disarm attribute");
                                    mqtt_area.code_disarm = code_disarm;
                                    break;
                                case "arm_home":
                                    if (!bool.TryParse(attributes["arm_home"], out bool arm_home))
                                        throw new Exception("Invalid arm_home attribute");
                                    mqtt_area.arm_home = arm_home;
                                    break;
                                case "arm_away":
                                    if (!bool.TryParse(attributes["arm_away"], out bool arm_away))
                                        throw new Exception("Invalid arm_away attribute");
                                    mqtt_area.arm_away = arm_away;
                                    break;
                                case "arm_night":
                                    if (!bool.TryParse(attributes["arm_night"], out bool arm_night))
                                        throw new Exception("Invalid arm_night attribute");
                                    mqtt_area.arm_night = arm_night;
                                    break;
                                case "arm_vacation":
                                    if (!bool.TryParse(attributes["arm_vacation"], out bool arm_vacation))
                                        throw new Exception("Invalid arm_vacation attribute");
                                    mqtt_area.arm_vacation = arm_vacation;
                                    break;
                                default:
                                    throw new Exception($"Unknown attribute {attribute}" );
                            }
                        }
                    }

                    ret.TryAdd(attrib_id, override_area);
                }

                return ret;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Invalid override area specified for {section}", section);
                throw;
            }
        }

        private static ConcurrentDictionary<int, T> LoadOverrideZone<T>(this NameValueCollection settings, string section) where T : new()
        {
            try
            {
                ConcurrentDictionary<int, T> ret = new ConcurrentDictionary<int, T>();

                string value = settings.CheckEnv(section);

                if (string.IsNullOrEmpty(value))
                    return ret;

                string[] ids = value.Split(',');

                for (int i = 0; i < ids.Length; i++)
                {
                    Dictionary<string, string> attributes = ids[i].TrimEnd(new char[] { ';' }).Split(';')
                        .Select(s => s.Split('='))
                        .ToDictionary(a => a[0].Trim(), a => a[1].Trim(), StringComparer.InvariantCultureIgnoreCase);

                    if (!attributes.ContainsKey("id") || !int.TryParse(attributes["id"], out int attrib_id))
                        throw new Exception("Missing or invalid id attribute");

                    T override_zone = new T();

                    if (override_zone is WebAPI.OverrideZone webapi_zone)
                    {
                        if (!attributes.ContainsKey("device_type") || !Enum.TryParse(attributes["device_type"], out WebAPI.DeviceType attrib_device_type))
                            throw new Exception("Missing or invalid device_type attribute");

                        webapi_zone.device_type = attrib_device_type;
                    }
                    else if (override_zone is MQTT.OverrideZone mqtt_zone)
                    {
                        if (!attributes.ContainsKey("device_class") || !Enum.TryParse(attributes["device_class"], out ha.BinarySensor.DeviceClass attrib_device_class))
                            throw new Exception("Missing or invalid device_class attribute");

                        mqtt_zone.device_class = attrib_device_class;
                    }

                    ret.TryAdd(attrib_id, override_zone);
                }

                return ret;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Invalid override zone specified for {section}", section);
                throw;
            }
        }

        private static ConcurrentDictionary<int, T> LoadOverrideUnit<T>(this NameValueCollection settings, string section) where T : new()
        {
            try
            {
                ConcurrentDictionary<int, T> ret = new ConcurrentDictionary<int, T>();

                string value = settings.CheckEnv(section);

                if (string.IsNullOrEmpty(value))
                    return ret;

                string[] ids = value.Split(',');

                for (int i = 0; i < ids.Length; i++)
                {
                    Dictionary<string, string> attributes = ids[i].TrimEnd(new char[] { ';' }).Split(';')
                        .Select(s => s.Split('='))
                        .ToDictionary(a => a[0].Trim(), a => a[1].Trim(), StringComparer.InvariantCultureIgnoreCase);

                    if (!attributes.ContainsKey("id") || !int.TryParse(attributes["id"], out int attrib_id))
                        throw new Exception("Missing or invalid id attribute");

                    T override_unit = new T();

                    if (override_unit is MQTT.OverrideUnit mqtt_unit)
                    {
                        if (!attributes.ContainsKey("type") || !Enum.TryParse(attributes["type"], out MQTT.UnitType attrib_type))
                            throw new Exception("Missing or invalid type attribute");

                        mqtt_unit.type = attrib_type;
                    }

                    ret.TryAdd(attrib_id, override_unit);
                }

                return ret;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Invalid override unit specified for {section}", section);
                throw;
            }
        }

        private static string ValidateHasValue(this NameValueCollection settings, string section)
        {
            string value = settings.CheckEnv(section);

            if(string.IsNullOrEmpty(value))
            {
                log.Error("Empty string specified for {section}", section);
                throw new Exception();
            }

            return value;
        }

        private static string ValidateEncryptionKey(this NameValueCollection settings, string section)
        {
            string value = settings.CheckEnv(section, true).Replace("-","");

            if (string.IsNullOrEmpty(value) || value.Length != 16)
            {
                log.Error("Invalid encryption key specified for {section}", section);
                throw new Exception();
            }

            return value;
        }

        private static int ValidateInt(this NameValueCollection settings, string section)
        {
            try
            {
                return int.Parse(settings.CheckEnv(section));
            }
            catch
            {
                log.Error("Invalid integer specified for {section}", section);
                throw;
            }
        }

        private static HashSet<int> ValidateRange(this NameValueCollection settings, string section)
        {
            try
            {
                string value = settings.CheckEnv(section);

                if (string.IsNullOrEmpty(value))
                    return new HashSet<int>();

                return new HashSet<int>(settings.CheckEnv(section).ParseRanges());
            }
            catch
            {
                log.Error("Invalid range specified for {section}", section);
                throw;
            }
        }

        private static int ValidatePort(this NameValueCollection settings, string section)
        {
            try
            {
                int port = int.Parse(settings.CheckEnv(section));

                if (port < 1 || port > 65534)
                    throw new Exception();

                return port;
            }
            catch
            {
                log.Error("Invalid port specified for {section}", section);
                throw;
            }
        }

        private static MailAddress ValidateMailFrom(this NameValueCollection settings, string section)
        {
            try
            {
                return new MailAddress(settings.CheckEnv(section));
            }
            catch
            {
                log.Error("Invalid email specified for {section}", section);
                throw;
            }
        }

        private static MailAddress[] ValidateMailTo(this NameValueCollection settings, string section)
        {
            try
            {
                string value = settings.CheckEnv(section);

                if (string.IsNullOrEmpty(value))
                    return new MailAddress[] {};

                string[] emails = value.Split(',');
                MailAddress[] addresses = new MailAddress[emails.Length];

                for(int i=0; i < emails.Length; i++)
                    addresses[i] = new MailAddress(emails[i]);

                return addresses;
            }
            catch
            {
                log.Error("Invalid email specified for {section}", section);
                throw;
            }
        }

        private static string[] ValidateMultipleStrings(this NameValueCollection settings, string section, bool sensitive = false)
        {
            try
            {
                if (settings.CheckEnv(section, true) == null)
                    return new string[] { };

                return settings.CheckEnv(section, sensitive).Split(',');
            }
            catch
            {
                log.Error("Invalid string specified for {section}", section);
                throw;
            }
        }

        private static bool ValidateBool (this NameValueCollection settings, string section)
        {
            string value = settings.CheckEnv(section);

            if (value == null)
                return false;
            if (string.Compare(value, "yes", true) == 0 || string.Compare(value, "true", true) == 0)
                return true;
            else if (string.Compare(value, "no", true) == 0 || string.Compare(value, "false", true) == 0)
                return false;
            else
            {
                log.Error("Invalid yes/no or true/false specified for {section}", section);
                throw new Exception();
            }
        }

        private static Type ValidateType(this NameValueCollection settings, string section, params Type[] types)
        {
            string value = settings.CheckEnv(section);

            if (value == null)
                return types[0];

            foreach (Type type in types)
                if (string.Compare(value, type.Name, true) == 0)
                    return type;

            log.Error("Invalid type specified for {section}", section);
            throw new Exception();
        }

        private static NameValueCollection LoadCollection(string[] lines)
        {
            NameValueCollection settings = new NameValueCollection();

            foreach(string line in lines)
            {
                if (line.StartsWith("#"))
                    continue;

                int pos = line.IndexOf('=', 0);

                if (pos == -1)
                    continue;

                string key = line.Substring(0, pos).Trim();
                string value = line.Substring(pos + 1).Trim();

                settings.Add(key, value);
            }

            return settings;
        }

        private static NameValueCollection LoadCollection(string file)
        {
            if (Global.DebugSettings)
                log.Debug("Using settings file {file}", file);

            if(!File.Exists(file))
            {
                log.Warning("Unable to locate settings file {file}", file);
                return new NameValueCollection();
            }

            try
            {
                return LoadCollection(File.ReadAllLines(file));
            }
            catch (FileNotFoundException ex)
            {
                log.Error(ex, "Error parsing settings file {file}", file);
                throw;
            }
        }
    }
}
