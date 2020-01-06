using HAI_Shared;
using OmniLinkBridge.OmniLink;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OmniLinkBridge.MQTT
{
    public class MessageProcessor
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Regex regexTopic = new Regex(Global.mqtt_prefix + "/([A-Za-z]+)([0-9]+)/(.*)", RegexOptions.Compiled);

        private IOmniLinkII OmniLink { get; set; }

        public MessageProcessor(IOmniLinkII omni)
        {
            OmniLink = omni;
        }

        public void Process(string messageTopic, string payload)
        {
            Match match = regexTopic.Match(messageTopic);

            if (!match.Success)
                return;

            if (!Enum.TryParse(match.Groups[1].Value, true, out CommandTypes type) 
                || !Enum.TryParse(match.Groups[3].Value, true, out Topic topic) 
                || !ushort.TryParse(match.Groups[2].Value, out ushort id))
                return;

            log.Debug("Received: Type: {type}, Id: {id}, Command: {command}, Value: {value}",
                type.ToString(), id, topic.ToString(), payload);

            if (type == CommandTypes.area && id <= OmniLink.Controller.Areas.Count)
                ProcessAreaReceived(OmniLink.Controller.Areas[id], topic, payload);
            else if (type == CommandTypes.zone && id > 0 && id <= OmniLink.Controller.Zones.Count)
                ProcessZoneReceived(OmniLink.Controller.Zones[id], topic, payload);
            else if (type == CommandTypes.unit && id > 0 && id <= OmniLink.Controller.Units.Count)
                ProcessUnitReceived(OmniLink.Controller.Units[id], topic, payload);
            else if (type == CommandTypes.thermostat && id > 0 && id <= OmniLink.Controller.Thermostats.Count)
                ProcessThermostatReceived(OmniLink.Controller.Thermostats[id], topic, payload);
            else if (type == CommandTypes.button && id > 0 && id <= OmniLink.Controller.Buttons.Count)
                ProcessButtonReceived(OmniLink.Controller.Buttons[id], topic, payload);
            else if (type == CommandTypes.message && id > 0 && id <= OmniLink.Controller.Messages.Count)
                ProcessMessageReceived(OmniLink.Controller.Messages[id], topic, payload);
        }

        private static readonly IDictionary<AreaCommands, enuUnitCommand> AreaMapping = new Dictionary<AreaCommands, enuUnitCommand>
        {
            { AreaCommands.disarm, enuUnitCommand.SecurityOff },
            { AreaCommands.arm_home, enuUnitCommand.SecurityDay },
            { AreaCommands.arm_away, enuUnitCommand.SecurityAway },
            { AreaCommands.arm_night, enuUnitCommand.SecurityNight },
            // The below aren't supported by Home Assistant
            { AreaCommands.arm_home_instant, enuUnitCommand.SecurityDyi },
            { AreaCommands.arm_night_delay, enuUnitCommand.SecurityNtd },
            { AreaCommands.arm_vacation, enuUnitCommand.SecurityVac },
        };

        private void ProcessAreaReceived(clsArea area, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out AreaCommands cmd))
            {
                if (area.Number == 0)
                    log.Debug("SetArea: 0 implies all areas will be changed");

                log.Debug("SetArea: {id} to {value}", area.Number, cmd.ToString().Replace("arm_", "").Replace("_", " "));
                OmniLink.SendCommand(AreaMapping[cmd], 0, (ushort)area.Number);
            }
        }

        private static readonly IDictionary<ZoneCommands, enuUnitCommand> ZoneMapping = new Dictionary<ZoneCommands, enuUnitCommand>
        {
            { ZoneCommands.restore, enuUnitCommand.Restore },
            { ZoneCommands.bypass, enuUnitCommand.Bypass },
        };

        private void ProcessZoneReceived(clsZone zone, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out ZoneCommands cmd))
            {
                log.Debug("SetZone: {id} to {value}", zone.Number, payload);
                OmniLink.SendCommand(ZoneMapping[cmd], 0, (ushort)zone.Number);
            }
        }

        private static readonly IDictionary<UnitCommands, enuUnitCommand> UnitMapping = new Dictionary<UnitCommands, enuUnitCommand>
        {
            { UnitCommands.OFF, enuUnitCommand.Off },
            { UnitCommands.ON, enuUnitCommand.On }
        };

        private void ProcessUnitReceived(clsUnit unit, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out UnitCommands cmd))
            {
                if (string.Compare(unit.ToState(), cmd.ToString()) != 0)
                {
                    log.Debug("SetUnit: {id} to {value}", unit.Number, cmd.ToString());
                    OmniLink.SendCommand(UnitMapping[cmd], 0, (ushort)unit.Number);
                }
            }
            else if (command == Topic.brightness_command && int.TryParse(payload, out int unitValue))
            {
                log.Debug("SetUnit: {id} to {value}%", unit.Number, payload);

                OmniLink.SendCommand(enuUnitCommand.Level, BitConverter.GetBytes(unitValue)[0], (ushort)unit.Number);

                // Force status change instead of waiting on controller to update
                // Home Assistant sends brightness immediately followed by ON,
                // which will cause light to go to 100% brightness
                unit.Status = (byte)(100 + unitValue);
            }
        }

        private void ProcessThermostatReceived(clsThermostat thermostat, Topic command, string payload)
        {
            if (command == Topic.temperature_heat_command && double.TryParse(payload, out double tempLow))
            {
                string tempUnit = "C";
                if (OmniLink.Controller.TempFormat == enuTempFormat.Fahrenheit)
                {
                    tempLow = tempLow.ToCelsius();
                    tempUnit = "F";
                }

                int temp = tempLow.ToOmniTemp();
                log.Debug("SetThermostatHeatSetpoint: {id} to {value}{temperatureUnit} ({temp})",
                    thermostat.Number, payload, tempUnit, temp);
                OmniLink.SendCommand(enuUnitCommand.SetLowSetPt, BitConverter.GetBytes(temp)[0], (ushort)thermostat.Number);
            }
            else if (command == Topic.temperature_cool_command && double.TryParse(payload, out double tempHigh))
            {
                string tempUnit = "C";
                if (OmniLink.Controller.TempFormat == enuTempFormat.Fahrenheit)
                {
                    tempHigh = tempHigh.ToCelsius();
                    tempUnit = "F";
                }

                int temp = tempHigh.ToOmniTemp();
                log.Debug("SetThermostatCoolSetpoint: {id} to {value}{temperatureUnit} ({temp})",
                    thermostat.Number, payload, tempUnit, temp);
                OmniLink.SendCommand(enuUnitCommand.SetHighSetPt, BitConverter.GetBytes(temp)[0], (ushort)thermostat.Number);
            }
            else if (command == Topic.humidify_command && double.TryParse(payload, out double humidify))
            {
                // Humidity is reported where Fahrenheit temperatures 0-100 correspond to 0-100% relative humidity
                int level = humidify.ToCelsius().ToOmniTemp();
                log.Debug("SetThermostatHumidifySetpoint: {id} to {value}% ({level})", thermostat.Number, payload, level);
                OmniLink.SendCommand(enuUnitCommand.SetHumidifySetPt, BitConverter.GetBytes(level)[0], (ushort)thermostat.Number);
            }
            else if (command == Topic.dehumidify_command && double.TryParse(payload, out double dehumidify))
            {
                int level = dehumidify.ToCelsius().ToOmniTemp();
                log.Debug("SetThermostatDehumidifySetpoint: {id} to {value}% ({level})", thermostat.Number, payload, level);
                OmniLink.SendCommand(enuUnitCommand.SetDeHumidifySetPt, BitConverter.GetBytes(level)[0], (ushort)thermostat.Number);
            }
            else if (command == Topic.mode_command && Enum.TryParse(payload, true, out enuThermostatMode mode))
            {
                log.Debug("SetThermostatMode: {id} to {value}", thermostat.Number, payload);
                OmniLink.SendCommand(enuUnitCommand.Mode, BitConverter.GetBytes((int)mode)[0], (ushort)thermostat.Number);
            }
            else if (command == Topic.fan_mode_command && Enum.TryParse(payload, true, out enuThermostatFanMode fanMode))
            {
                log.Debug("SetThermostatFanMode: {id} to {value}", thermostat.Number, payload);
                OmniLink.SendCommand(enuUnitCommand.Fan, BitConverter.GetBytes((int)fanMode)[0], (ushort)thermostat.Number);
            }
            else if (command == Topic.hold_command && Enum.TryParse(payload, true, out enuThermostatHoldMode holdMode))
            {
                log.Debug("SetThermostatHold: {id} to {value}", thermostat.Number, payload);
                OmniLink.SendCommand(enuUnitCommand.Hold, BitConverter.GetBytes((int)holdMode)[0], (ushort)thermostat.Number);
            }
        }

        private void ProcessButtonReceived(clsButton button, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out UnitCommands cmd) && cmd == UnitCommands.ON)
            {
                log.Debug("PushButton: {id}", button.Number);
                OmniLink.SendCommand(enuUnitCommand.Button, 0, (ushort)button.Number);
            }
        }

        private static readonly IDictionary<MessageCommands, enuUnitCommand> MessageMapping = new Dictionary<MessageCommands, enuUnitCommand>
        {
            { MessageCommands.show, enuUnitCommand.ShowMsgWBeep },
            { MessageCommands.show_no_beep, enuUnitCommand.ShowMsgNoBeep },
            { MessageCommands.show_no_beep_or_led, enuUnitCommand.ShowMsgNoBeep },
            { MessageCommands.clear, enuUnitCommand.ClearMsg },
        };

        private void ProcessMessageReceived(clsMessage message, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out MessageCommands cmd))
            {
                log.Debug("SetMessage: {id} to {value}", message.Number, cmd.ToString().Replace("_", " "));

                byte par = 0;
                if (cmd == MessageCommands.show_no_beep)
                    par = 1;
                else if (cmd == MessageCommands.show_no_beep_or_led)
                    par = 2;

                OmniLink.SendCommand(MessageMapping[cmd], par, (ushort)message.Number);
            }
        }
    }
}
