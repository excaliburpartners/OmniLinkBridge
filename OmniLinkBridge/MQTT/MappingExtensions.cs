using HAI_Shared;
using Newtonsoft.Json;

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
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}";
            ret.name = Global.mqtt_discovery_name_prefix + area.Name;
            ret.state_topic = area.ToTopic(Topic.basic_state);
            ret.command_topic = area.ToTopic(Topic.command);
            return ret;
        }

        public static string ToState(this clsArea area)
        {
            if (area.AreaAlarms.IsBitSet(0) ||  // Burgulary
                area.AreaAlarms.IsBitSet(3) ||  // Auxiliary
                area.AreaAlarms.IsBitSet(6))    // Duress
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
            if (area.AreaAlarms.IsBitSet(0) ||  // Burgulary
                area.AreaAlarms.IsBitSet(3) ||  // Auxiliary
                area.AreaAlarms.IsBitSet(6))    // Duress
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

        public static BinarySensor ToConfigBurglary(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}burglary";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Burglary";
            ret.device_class = BinarySensor.DeviceClass.safety;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.burglary_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigFire(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}fire";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Fire";
            ret.device_class = BinarySensor.DeviceClass.smoke;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.fire_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigGas(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}gas";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Gas";
            ret.device_class = BinarySensor.DeviceClass.gas;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.gas_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigAux(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}auxiliary";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Auxiliary";
            ret.device_class = BinarySensor.DeviceClass.problem;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if  value_json.burglary_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigFreeze(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}freeze";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Freeze";
            ret.device_class = BinarySensor.DeviceClass.cold;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.freeze_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigWater(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}water";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Water";
            ret.device_class = BinarySensor.DeviceClass.moisture;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.water_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigDuress(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}duress";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Duress";
            ret.device_class = BinarySensor.DeviceClass.safety;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.duress_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static BinarySensor ToConfigTemp(this clsArea area)
        {
            BinarySensor ret = new BinarySensor();
            ret.unique_id = $"{Global.mqtt_prefix}area{area.Number.ToString()}temp";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{area.Name} Temp";
            ret.device_class = BinarySensor.DeviceClass.heat;
            ret.state_topic = area.ToTopic(Topic.json_state);
            ret.value_template = "{% if value_json.temperature_alarm %} ON {%- else -%} OFF {%- endif %}";
            return ret;
        }

        public static string ToJsonState(this clsArea area)
        {
            AreaState state = new AreaState()
            {
                arming = area.ExitTimer > 0,
                burglary_alarm = area.AreaAlarms.IsBitSet(0),
                fire_alarm = area.AreaAlarms.IsBitSet(1),
                gas_alarm = area.AreaAlarms.IsBitSet(2),
                auxiliary_alarm = area.AreaAlarms.IsBitSet(3),
                freeze_alarm = area.AreaAlarms.IsBitSet(4),
                water_alarm = area.AreaAlarms.IsBitSet(5),
                duress_alarm = area.AreaAlarms.IsBitSet(6),
                temperature_alarm = area.AreaAlarms.IsBitSet(7)
            };

            switch (area.AreaMode)
            {
                case enuSecurityMode.Night:
                    state.mode = "night";
                    break;
                case enuSecurityMode.NightDly:
                    state.mode = "night_delay";
                    break;
                case enuSecurityMode.Day:
                    state.mode = "home";
                    break;
                case enuSecurityMode.DayInst:
                    state.mode = "home_instant";
                    break;
                case enuSecurityMode.Away:
                    state.mode = "away";
                    break;
                case enuSecurityMode.Vacation:
                    state.mode = "vacation";
                    break;
                case enuSecurityMode.Off:
                default:
                    state.mode = "off";
                    break;
            }

            return JsonConvert.SerializeObject(state);
        }

        public static string ToTopic(this clsZone zone, Topic topic)
        {
            return $"{Global.mqtt_prefix}/zone{zone.Number.ToString()}/{topic.ToString()}";
        }

        public static Sensor ToConfigTemp(this clsZone zone, enuTempFormat format)
        {
            Sensor ret = new Sensor();
            ret.unique_id = $"{Global.mqtt_prefix}zone{zone.Number.ToString()}temp";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{zone.Name} Temp";
            ret.device_class = Sensor.DeviceClass.temperature;
            ret.state_topic = zone.ToTopic(Topic.current_temperature);
            ret.unit_of_measurement = (format == enuTempFormat.Fahrenheit ? "°F" : "°C");
            return ret;
        }

        public static Sensor ToConfigHumidity(this clsZone zone)
        {
            Sensor ret = new Sensor();
            ret.unique_id = $"{Global.mqtt_prefix}zone{zone.Number.ToString()}humidity";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{zone.Name} Humidity";
            ret.device_class = Sensor.DeviceClass.humidity;
            ret.state_topic = zone.ToTopic(Topic.current_humidity);
            ret.unit_of_measurement = "%";
            return ret;
        }

        public static Sensor ToConfigSensor(this clsZone zone)
        {
            Sensor ret = new Sensor();
            ret.unique_id = $"{Global.mqtt_prefix}zone{zone.Number.ToString()}";
            ret.name = Global.mqtt_discovery_name_prefix + zone.Name;

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
            ret.unique_id = $"{Global.mqtt_prefix}zone{zone.Number.ToString()}binary";
            ret.name = Global.mqtt_discovery_name_prefix + zone.Name;

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
            ret.unique_id = $"{Global.mqtt_prefix}unit{unit.Number.ToString()}light";
            ret.name = Global.mqtt_discovery_name_prefix + unit.Name;
            ret.state_topic = unit.ToTopic(Topic.state);
            ret.command_topic = unit.ToTopic(Topic.command);
            ret.brightness_state_topic = unit.ToTopic(Topic.brightness_state);
            ret.brightness_command_topic = unit.ToTopic(Topic.brightness_command);
            return ret;
        }

        public static Switch ToConfigSwitch(this clsUnit unit)
        {
            Switch ret = new Switch();
            ret.unique_id = $"{Global.mqtt_prefix}unit{unit.Number.ToString()}switch";
            ret.name = Global.mqtt_discovery_name_prefix + unit.Name;
            ret.state_topic = unit.ToTopic(Topic.state);
            ret.command_topic = unit.ToTopic(Topic.command);
            return ret;
        }

        public static string ToState(this clsUnit unit)
        {
            return unit.Status == 0 || unit.Status == 100 ? UnitCommands.OFF.ToString() : UnitCommands.ON.ToString();
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

        public static Sensor ToConfigTemp(this clsThermostat thermostat, enuTempFormat format)
        {
            Sensor ret = new Sensor();
            ret.unique_id = $"{Global.mqtt_prefix}thermostat{thermostat.Number.ToString()}temp";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{thermostat.Name} Temp";
            ret.device_class = Sensor.DeviceClass.temperature;
            ret.state_topic = thermostat.ToTopic(Topic.current_temperature);
            ret.unit_of_measurement = (format == enuTempFormat.Fahrenheit ? "°F" : "°C");
            return ret;
        }

        public static Sensor ToConfigHumidity(this clsThermostat thermostat)
        {
            Sensor ret = new Sensor();
            ret.unique_id = $"{Global.mqtt_prefix}thermostat{thermostat.Number.ToString()}humidity";
            ret.name = $"{Global.mqtt_discovery_name_prefix}{thermostat.Name} Humidity";
            ret.device_class = Sensor.DeviceClass.humidity;
            ret.state_topic = thermostat.ToTopic(Topic.current_humidity);
            ret.unit_of_measurement = "%";
            return ret;
        }

        public static Climate ToConfig(this clsThermostat thermostat, enuTempFormat format)
        {
            Climate ret = new Climate();

            if(format == enuTempFormat.Celsius)
            {
                ret.min_temp = "7";
                ret.max_temp = "35";
            }

            ret.unique_id = $"{Global.mqtt_prefix}thermostat{thermostat.Number.ToString()}";
            ret.name = Global.mqtt_discovery_name_prefix + thermostat.Name;
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
            ret.unique_id = $"{Global.mqtt_prefix}button{button.Number.ToString()}";
            ret.name = Global.mqtt_discovery_name_prefix + button.Name;
            ret.state_topic = button.ToTopic(Topic.state);
            ret.command_topic = button.ToTopic(Topic.command);
            return ret;
        }

        public static string ToTopic(this clsMessage message, Topic topic)
        {
            return $"{Global.mqtt_prefix}/message{message.Number.ToString()}/{topic.ToString()}";
        }

        public static string ToState(this clsMessage message)
        {
            if (message.Status == enuMessageStatus.Displayed)
                return "displayed";
            else if (message.Status == enuMessageStatus.NotAcked)
                return "displayed_not_acknowledged";
            else
                return "off";
        }
    }
}
