using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HAI_Shared;

namespace OmniLinkBridge.MQTT
{
    public static class MappingExtensions
    {
        public static string ToTopic(this clsArea area, Topic topic)
        {
            return $"{Global.mqtt_prefix}/area{area.Number.ToString()}/{topic.ToString()}";
        }

        public static Alarm ToConfig(this clsArea area)
        {
            Alarm ret = new Alarm();
            ret.name = area.Name;
            ret.state_topic = area.ToTopic(Topic.basic_state);
            ret.command_topic = area.ToTopic(Topic.command);
            return ret;
        }

        public static string ToState(this clsArea area)
        {
            if (area.AreaBurglaryAlarmText != "OK")
                return "triggered";
            else if (area.ExitTimer > 0)
                return "pending";

            switch (area.AreaMode)
            {
                case enuSecurityMode.Night:
                    return "armed_night";
                case enuSecurityMode.NightDly:
                    return "armed_night_delay";
                case enuSecurityMode.Day:
                    return "armed_home";
                case enuSecurityMode.DayInst:
                    return "armed_home_instant";
                case enuSecurityMode.Away:
                    return "armed_away";
                case enuSecurityMode.Vacation:
                    return "armed_vacation";
                case enuSecurityMode.Off:
                default:
                    return "disarmed";
            }
        }

        public static string ToBasicState(this clsArea area)
        {
            if (area.AreaBurglaryAlarmText != "OK")
                return "triggered";
            else if (area.ExitTimer > 0)
                return "pending";

            switch (area.AreaMode)
            {
                case enuSecurityMode.Night:
                case enuSecurityMode.NightDly:
                    return "armed_night";
                case enuSecurityMode.Day:
                case enuSecurityMode.DayInst:
                    return "armed_home";
                case enuSecurityMode.Away:
                case enuSecurityMode.Vacation:
                    return "armed_away";
                case enuSecurityMode.Off:
                default:
                    return "disarmed";
            }
        }

        public static string ToTopic(this clsZone zone, Topic topic)
        {
            return $"{Global.mqtt_prefix}/zone{zone.Number.ToString()}/{topic.ToString()}";
        }

        public static Sensor ToConfigTemp(this clsZone zone)
        {
            Sensor ret = new Sensor();
            ret.name = zone.Name;
            ret.device_class = Sensor.DeviceClass.temperature;
            ret.state_topic = zone.ToTopic(Topic.current_temperature);
            ret.unit_of_measurement = "°F";
            return ret;
        }

        public static Sensor ToConfigHumidity(this clsZone zone)
        {
            Sensor ret = new Sensor();
            ret.name = zone.Name;
            ret.device_class = Sensor.DeviceClass.humidity;
            ret.state_topic = zone.ToTopic(Topic.current_humidity);
            ret.unit_of_measurement = "%";
            return ret;
        }

        public static Sensor ToConfigSensor(this clsZone zone)
        {
            Sensor ret = new Sensor();
            ret.name = zone.Name;

            switch (zone.ZoneType)
            {
                case enuZoneType.EntryExit:
                case enuZoneType.X2EntryDelay:
                case enuZoneType.X4EntryDelay:
                    ret.icon = "mdi:door";
                    break;
                case enuZoneType.Perimeter:
                    ret.icon = "mdi:window-closed";
                    break;
                case enuZoneType.Tamper:
                    ret.icon = "mdi:shield";
                    break;
                case enuZoneType.AwayInt:
                case enuZoneType.NightInt:
                    ret.icon = "mdi:walk";
                    break;
                case enuZoneType.Water:
                    ret.icon = "mdi:water";
                    break;
                case enuZoneType.Fire:
                    ret.icon = "mdi:fire";
                    break;
                case enuZoneType.Gas:
                    ret.icon = "mdi:gas-cylinder";
                    break;
            }

            ret.value_template = @"{{ value|replace(""_"", "" "")|title }}";

            ret.state_topic = zone.ToTopic(Topic.state);
            return ret;
        }

        public static BinarySensor ToConfig(this clsZone zone)
        {
            BinarySensor ret = new BinarySensor();
            ret.name = zone.Name;

            Global.mqtt_discovery_override_zone.TryGetValue(zone.Number, out OverrideZone override_zone);

            if (override_zone != null)
            {
                ret.device_class = override_zone.device_class;
            }
            else
            {
                switch (zone.ZoneType)
                {
                    case enuZoneType.EntryExit:
                    case enuZoneType.X2EntryDelay:
                    case enuZoneType.X4EntryDelay:
                        ret.device_class = BinarySensor.DeviceClass.door;
                        break;
                    case enuZoneType.Perimeter:
                        ret.device_class = BinarySensor.DeviceClass.window;
                        break;
                    case enuZoneType.Tamper:
                        ret.device_class = BinarySensor.DeviceClass.problem;
                        break;
                    case enuZoneType.AwayInt:
                    case enuZoneType.NightInt:
                        ret.device_class = BinarySensor.DeviceClass.motion;
                        break;
                    case enuZoneType.Water:
                        ret.device_class = BinarySensor.DeviceClass.moisture;
                        break;
                    case enuZoneType.Fire:
                        ret.device_class = BinarySensor.DeviceClass.smoke;
                        break;
                    case enuZoneType.Gas:
                        ret.device_class = BinarySensor.DeviceClass.gas;
                        break;
                }
            }

            ret.state_topic = zone.ToTopic(Topic.basic_state);
            return ret;
        }

        public static string ToState(this clsZone zone)
        {
            if (zone.Status.IsBitSet(5))
                return "bypassed";
            else if (zone.Status.IsBitSet(2))
                return "tripped";
            else if (zone.Status.IsBitSet(4))
                return "armed";
            else if (zone.Status.IsBitSet(1))
                return "trouble";
            else if (zone.Status.IsBitSet(0))
                return "not_ready";
            else
                return "secure";
        }

        public static string ToBasicState(this clsZone zone)
        {
            return zone.Status.IsBitSet(0) ? "ON" : "OFF";
        }

        public static string ToTopic(this clsUnit unit, Topic topic)
        {
            return $"{Global.mqtt_prefix}/unit{unit.Number.ToString()}/{topic.ToString()}";
        }

        public static Light ToConfig(this clsUnit unit)
        {
            Light ret = new Light();
            ret.name = unit.Name;
            ret.state_topic = unit.ToTopic(Topic.state);
            ret.command_topic = unit.ToTopic(Topic.command);
            ret.brightness_state_topic = unit.ToTopic(Topic.brightness_state);
            ret.brightness_command_topic = unit.ToTopic(Topic.brightness_command);
            return ret;
        }

        public static Switch ToConfigSwitch(this clsUnit unit)
        {
            Switch ret = new Switch();
            ret.name = unit.Name;
            ret.state_topic = unit.ToTopic(Topic.state);
            ret.command_topic = unit.ToTopic(Topic.command);
            return ret;
        }

        public static string ToState(this clsUnit unit)
        {
            return unit.Status == 0 || unit.Status == 100 ? "OFF" : "ON";
        }

        public static int ToBrightnessState(this clsUnit unit)
        {
            if (unit.Status > 100)
                return (ushort)(unit.Status - 100);
            else if (unit.Status == 1)
                return 100;
            else
                return 0;
        }

        public static string ToTopic(this clsThermostat thermostat, Topic topic)
        {
            return $"{Global.mqtt_prefix}/thermostat{thermostat.Number.ToString()}/{topic.ToString()}";
        }

        public static Sensor ToConfigHumidity(this clsThermostat zone)
        {
            Sensor ret = new Sensor();
            ret.name = zone.Name;
            ret.device_class = Sensor.DeviceClass.humidity;
            ret.state_topic = zone.ToTopic(Topic.current_humidity);
            ret.unit_of_measurement = "%";
            return ret;
        }

        public static Climate ToConfig(this clsThermostat thermostat)
        {
            Climate ret = new Climate();
            ret.name = thermostat.Name;
            ret.current_temperature_topic = thermostat.ToTopic(Topic.current_temperature);

            ret.temperature_low_state_topic = thermostat.ToTopic(Topic.temperature_heat_state);
            ret.temperature_low_command_topic = thermostat.ToTopic(Topic.temperature_heat_command);

            ret.temperature_high_state_topic = thermostat.ToTopic(Topic.temperature_cool_state);
            ret.temperature_high_command_topic = thermostat.ToTopic(Topic.temperature_cool_command);

            ret.mode_state_topic = thermostat.ToTopic(Topic.mode_state);
            ret.mode_command_topic = thermostat.ToTopic(Topic.mode_command);

            ret.fan_mode_state_topic = thermostat.ToTopic(Topic.fan_mode_state);
            ret.fan_mode_command_topic = thermostat.ToTopic(Topic.fan_mode_command);

            ret.hold_state_topic = thermostat.ToTopic(Topic.hold_state);
            ret.hold_command_topic = thermostat.ToTopic(Topic.hold_command);
            return ret;
        }

        public static string ToOperationState(this clsThermostat thermostat)
        {
            string status = thermostat.HorC_StatusText();

            if (status.Contains("COOLING"))
                return "cool";
            else if (status.Contains("HEATING"))
                return "heat";
            else
                return "idle";
        }

        public static string ToTopic(this clsButton button, Topic topic)
        {
            return $"{Global.mqtt_prefix}/button{button.Number.ToString()}/{topic.ToString()}";
        }

        public static Switch ToConfig(this clsButton button)
        {
            Switch ret = new Switch();
            ret.name = button.Name;
            ret.state_topic = button.ToTopic(Topic.state);
            ret.command_topic = button.ToTopic(Topic.command);
            return ret;
        }
    }
}
