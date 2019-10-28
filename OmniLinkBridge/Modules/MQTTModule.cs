using HAI_Shared;
using OmniLinkBridge.OmniLink;
using log4net;
using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using MQTTnet.Extensions.ManagedClient;
using OmniLinkBridge.MQTT;
using MQTTnet.Protocol;
using System.Text.RegularExpressions;
using System.Text;

namespace OmniLinkBridge.Modules
{
    public class MQTTModule : IModule
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OmniLinkII OmniLink { get; set; }
        private IManagedMqttClient MqttClient { get; set; }
        private bool ControllerConnected { get; set; }

        private Regex regexTopic = new Regex(Global.mqtt_prefix + "/([A-Za-z]+)([0-9]+)/(.*)", RegexOptions.Compiled);

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        public MQTTModule(OmniLinkII omni)
        {
            OmniLink = omni;
            OmniLink.OnConnect += OmniLink_OnConnect;
            OmniLink.OnAreaStatus += Omnilink_OnAreaStatus;
            OmniLink.OnZoneStatus += Omnilink_OnZoneStatus;
            OmniLink.OnUnitStatus += Omnilink_OnUnitStatus;
            OmniLink.OnThermostatStatus += Omnilink_OnThermostatStatus;
        }

        public void Startup()
        {
            MqttClientOptionsBuilder options = new MqttClientOptionsBuilder()
                .WithTcpServer(Global.mqtt_server);

            if (!string.IsNullOrEmpty(Global.mqtt_username))
                options = options
                    .WithCredentials(Global.mqtt_username, Global.mqtt_password);

            ManagedMqttClientOptions manoptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(options.Build())
                .Build();

            MqttClient = new MqttFactory().CreateManagedMqttClient();
            MqttClient.Connected += (sender, e) =>
            {
                log.Debug("Connected");

                // For the initial connection wait for the controller connected event to publish config
                // For subsequent connections publish config immediately
                if(ControllerConnected)
                    PublishConfig();
            };
            MqttClient.ConnectingFailed += (sender, e) => { log.Debug("Error connecting " + e.Exception.Message); };

            MqttClient.StartAsync(manoptions);

            MqttClient.ApplicationMessageReceived += MqttClient_ApplicationMessageReceived;

            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.brightness_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.temperature_heat_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.temperature_cool_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.humidify_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.dehumidify_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.mode_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.fan_mode_command}").Build());
            MqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{Topic.hold_command}").Build());

            // Wait until shutdown
            trigger.WaitOne();

            MqttClient.PublishAsync($"{Global.mqtt_prefix}/status", "offline", MqttQualityOfServiceLevel.AtMostOnce, true);
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            Match match = regexTopic.Match(e.ApplicationMessage.Topic);

            if (!match.Success)
                return;

            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            log.Debug($"Received: Type: {match.Groups[1].Value}, Id: {match.Groups[2].Value}, Command: {match.Groups[3].Value}, Value: {payload}");

            if (match.Groups[1].Value == "area" && ushort.TryParse(match.Groups[2].Value, out ushort areaId) && areaId < OmniLink.Controller.Areas.Count)
            {
                ProcessAreaReceived(OmniLink.Controller.Areas[areaId], match.Groups[3].Value, payload);
            }
            if (match.Groups[1].Value == "zone" && ushort.TryParse(match.Groups[2].Value, out ushort zoneId) && zoneId < OmniLink.Controller.Zones.Count)
            {
                ProcessZoneReceived(OmniLink.Controller.Zones[zoneId], match.Groups[3].Value, payload);
            }
            else if (match.Groups[1].Value == "unit" && ushort.TryParse(match.Groups[2].Value, out ushort unitId) && unitId < OmniLink.Controller.Units.Count)
            {
                ProcessUnitReceived(OmniLink.Controller.Units[unitId], match.Groups[3].Value, payload);
            }
            else if (match.Groups[1].Value == "thermostat" && ushort.TryParse(match.Groups[2].Value, out ushort thermostatId) && thermostatId < OmniLink.Controller.Thermostats.Count)
            {
                ProcessThermostatReceived(OmniLink.Controller.Thermostats[thermostatId], match.Groups[3].Value, payload);
            }
            else if (match.Groups[1].Value == "button" && ushort.TryParse(match.Groups[2].Value, out ushort buttonId) && buttonId < OmniLink.Controller.Buttons.Count)
            {
                ProcessButtonReceived(OmniLink.Controller.Buttons[buttonId], match.Groups[3].Value, payload);
            }
        }

        private void ProcessAreaReceived(clsArea area, string command, string payload)
        {
            if (string.Compare(command, Topic.command.ToString()) == 0)
            {
                if(string.Compare(payload, "arm_home", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to home");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityDay, 0, (ushort)area.Number);
                }
                else if (string.Compare(payload, "arm_away", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to away");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityAway, 0, (ushort)area.Number);
                }
                else if (string.Compare(payload, "arm_night", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to night");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityNight, 0, (ushort)area.Number);
                }
                else if (string.Compare(payload, "disarm", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to disarm");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityOff, 0, (ushort)area.Number);
                }
                // The below aren't supported by Home Assistant
                else if (string.Compare(payload, "arm_home_instant", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to home instant");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityDyi, 0, (ushort)area.Number);
                }
                else if (string.Compare(payload, "arm_night_delay", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to night delay");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityNtd, 0, (ushort)area.Number);
                }
                else if (string.Compare(payload, "arm_vacation", true) == 0)
                {
                    log.Debug("SetArea: " + area.Number + " to vacation");
                    OmniLink.Controller.SendCommand(enuUnitCommand.SecurityVac, 0, (ushort)area.Number);
                }
            }
        }

        private void ProcessZoneReceived(clsZone zone, string command, string payload)
        {
            if (string.Compare(command, Topic.command.ToString()) == 0)
            {
                if (string.Compare(payload, "bypass", true) == 0)
                {
                    log.Debug("SetZone: " + zone.Number + " to " + payload);
                    OmniLink.Controller.SendCommand(enuUnitCommand.Bypass, 0, (ushort)zone.Number);
                }
                else if (string.Compare(payload, "restore", true) == 0)
                {
                    log.Debug("SetZone: " + zone.Number + " to " + payload);
                    OmniLink.Controller.SendCommand(enuUnitCommand.Restore, 0, (ushort)zone.Number);
                }
            }
        }

        private void ProcessUnitReceived(clsUnit unit, string command, string payload)
        {
            if (string.Compare(command, Topic.command.ToString()) == 0 && (payload == "ON" || payload == "OFF"))
            {
                if (unit.ToState() != payload)
                {
                    log.Debug("SetUnit: " + unit.Number + " to " + payload);

                    if (payload == "ON")
                        OmniLink.Controller.SendCommand(enuUnitCommand.On, 0, (ushort)unit.Number);
                    else
                        OmniLink.Controller.SendCommand(enuUnitCommand.Off, 0, (ushort)unit.Number);
                }
            }
            else if (string.Compare(command, Topic.brightness_command.ToString()) == 0 && Int32.TryParse(payload, out int unitValue))
            {
                log.Debug("SetUnit: " + unit.Number + " to " + payload + "%");

                OmniLink.Controller.SendCommand(enuUnitCommand.Level, BitConverter.GetBytes(unitValue)[0], (ushort)unit.Number);

                // Force status change instead of waiting on controller to update
                // Home Assistant sends brightness immediately followed by ON,
                // which will cause light to go to 100% brightness
                unit.Status = (byte)(100 + unitValue);
            }
        }

        private void ProcessThermostatReceived(clsThermostat thermostat, string command, string payload)
        {
            if (string.Compare(command, Topic.temperature_heat_command.ToString()) == 0 && double.TryParse(payload, out double tempLow))
            {
                string tempUnit = "C";
                if (OmniLink.Controller.TempFormat == enuTempFormat.Fahrenheit)
                {
                    tempLow = tempLow.ToCelsius();
                    tempUnit = "F";
                }

                int temp = tempLow.ToOmniTemp();
                log.Debug("SetThermostatHeatSetpoint: " + thermostat.Number + " to " + payload + tempUnit + "(" + temp + ")");
                OmniLink.Controller.SendCommand(enuUnitCommand.SetLowSetPt, BitConverter.GetBytes(temp)[0], (ushort)thermostat.Number);
            }
            else if (string.Compare(command, Topic.temperature_cool_command.ToString()) == 0 && double.TryParse(payload, out double tempHigh))
            {
                string tempUnit = "C";
                if (OmniLink.Controller.TempFormat == enuTempFormat.Fahrenheit)
                {
                    tempHigh = tempHigh.ToCelsius();
                    tempUnit = "F";
                }

                int temp = tempHigh.ToOmniTemp();
                log.Debug("SetThermostatCoolSetpoint: " + thermostat.Number + " to " + payload + tempUnit + "(" + temp + ")");
                OmniLink.Controller.SendCommand(enuUnitCommand.SetHighSetPt, BitConverter.GetBytes(temp)[0], (ushort)thermostat.Number);
            }
            else if (string.Compare(command, Topic.humidify_command.ToString()) == 0 && double.TryParse(payload, out double humidify))
            {
                // Humidity is reported where Fahrenheit temperatures 0-100 correspond to 0-100% relative humidity
                int level = humidify.ToCelsius().ToOmniTemp();
                log.Debug("SetThermostatHumidifySetpoint: " + thermostat.Number + " to " + payload + "% (" + level + ")");
                OmniLink.Controller.SendCommand(enuUnitCommand.SetHumidifySetPt, BitConverter.GetBytes(level)[0], (ushort)thermostat.Number);
            }
            else if (string.Compare(command, Topic.dehumidify_command.ToString()) == 0 && double.TryParse(payload, out double dehumidify))
            {
                int level = dehumidify.ToCelsius().ToOmniTemp();
                log.Debug("SetThermostatDehumidifySetpoint: " + thermostat.Number + " to " + payload + "% (" + level + ")");
                OmniLink.Controller.SendCommand(enuUnitCommand.SetDeHumidifySetPt, BitConverter.GetBytes(level)[0], (ushort)thermostat.Number);
            }
            else if (string.Compare(command, Topic.mode_command.ToString()) == 0 && Enum.TryParse(payload, true, out enuThermostatMode mode))
            {
                log.Debug("SetThermostatMode: " + thermostat.Number + " to " + payload);
                OmniLink.Controller.SendCommand(enuUnitCommand.Mode, BitConverter.GetBytes((int)mode)[0], (ushort)thermostat.Number);
            }
            else if (string.Compare(command, Topic.fan_mode_command.ToString()) == 0 && Enum.TryParse(payload, true, out enuThermostatFanMode fanMode))
            {
                log.Debug("SetThermostatFanMode: " + thermostat.Number + " to " + payload);
                OmniLink.Controller.SendCommand(enuUnitCommand.Fan, BitConverter.GetBytes((int)fanMode)[0], (ushort)thermostat.Number);
            }
            else if (string.Compare(command, Topic.hold_command.ToString()) == 0 && Enum.TryParse(payload, true, out enuThermostatHoldMode holdMode))
            {
                log.Debug("SetThermostatHold: " + thermostat.Number + " to " + payload);
                OmniLink.Controller.SendCommand(enuUnitCommand.Hold, BitConverter.GetBytes((int)holdMode)[0], (ushort)thermostat.Number);
            }
        }

        private void ProcessButtonReceived(clsButton button, string command, string payload)
        {
            if (string.Compare(command, Topic.command.ToString()) == 0 && payload == "ON")
            {
                log.Debug("PushButton: " + button.Number);
                OmniLink.Controller.SendCommand(enuUnitCommand.Button, 0, (ushort)button.Number);
            }
        }

        public void Shutdown()
        {
            trigger.Set();
        }

        private void OmniLink_OnConnect(object sender, EventArgs e)
        {
            PublishConfig();

            ControllerConnected = true;
        }

        private void PublishConfig()
        {
            PublishAreas();
            PublishZones();
            PublishUnits();
            PublishThermostats();
            PublishButtons();

            MqttClient.PublishAsync($"{Global.mqtt_prefix}/status", "online", MqttQualityOfServiceLevel.AtMostOnce, true);
        }

        private void PublishAreas()
        {
            log.Debug("Publishing areas");

            for (ushort i = 1; i < OmniLink.Controller.Areas.Count; i++)
            {
                clsArea area = OmniLink.Controller.Areas[i];

                if (area.DefaultProperties == true)
                {
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/alarm_control_panel/{Global.mqtt_prefix}/area{i.ToString()}/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}burglary/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}fire/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}gas/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}aux/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}freeze/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}water/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}duress/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}temp/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    continue;
                }

                PublishAreaState(area);

                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/alarm_control_panel/{Global.mqtt_prefix}/area{i.ToString()}/config",
                    JsonConvert.SerializeObject(area.ToConfig()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}burglary/config",
                    JsonConvert.SerializeObject(area.ToConfigBurglary()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}fire/config",
                    JsonConvert.SerializeObject(area.ToConfigFire()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}gas/config",
                    JsonConvert.SerializeObject(area.ToConfigGas()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}aux/config",
                    JsonConvert.SerializeObject(area.ToConfigAux()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}freeze/config",
                    JsonConvert.SerializeObject(area.ToConfigFreeze()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}water/config",
                    JsonConvert.SerializeObject(area.ToConfigWater()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}duress/config",
                   JsonConvert.SerializeObject(area.ToConfigDuress()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}temp/config",
                   JsonConvert.SerializeObject(area.ToConfigTemp()), MqttQualityOfServiceLevel.AtMostOnce, true);
            }
        }

        private void PublishZones()
        {
            log.Debug("Publishing zones");

            for (ushort i = 1; i < OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == true || Global.mqtt_discovery_ignore_zones.Contains(zone.Number))
                {
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}temp/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}humidity/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    continue;
                }

                PublishZoneState(zone);

                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config",
                    JsonConvert.SerializeObject(zone.ToConfig()), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config",
                    JsonConvert.SerializeObject(zone.ToConfigSensor()), MqttQualityOfServiceLevel.AtMostOnce, true);

                if (zone.IsTemperatureZone())
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}temp/config",
                        JsonConvert.SerializeObject(zone.ToConfigTemp(OmniLink.Controller.TempFormat)), MqttQualityOfServiceLevel.AtMostOnce, true);
                else if (zone.IsHumidityZone())
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}humidity/config",
                        JsonConvert.SerializeObject(zone.ToConfigHumidity()), MqttQualityOfServiceLevel.AtMostOnce, true);
            }
        }

        private void PublishUnits()
        {
            log.Debug("Publishing units");

            for (ushort i = 1; i < OmniLink.Controller.Units.Count; i++)
            {
                clsUnit unit = OmniLink.Controller.Units[i];
                
                if (unit.DefaultProperties == true || Global.mqtt_discovery_ignore_units.Contains(unit.Number))
                {
                    string type = i < 385 ? "light" : "switch";
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/{type}/{Global.mqtt_prefix}/unit{i.ToString()}/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    continue;
                }

                PublishUnitState(unit);

                if(i < 385)
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/light/{Global.mqtt_prefix}/unit{i.ToString()}/config",
                        JsonConvert.SerializeObject(unit.ToConfig()), MqttQualityOfServiceLevel.AtMostOnce, true);
                else
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/unit{i.ToString()}/config",
                        JsonConvert.SerializeObject(unit.ToConfigSwitch()), MqttQualityOfServiceLevel.AtMostOnce, true);
            }
        }

        private void PublishThermostats()
        {
            log.Debug("Publishing thermostats");

            for (ushort i = 1; i < OmniLink.Controller.Thermostats.Count; i++)
            {
                clsThermostat thermostat = OmniLink.Controller.Thermostats[i];

                if (thermostat.DefaultProperties == true)
                {
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/climate/{Global.mqtt_prefix}/thermostat{i.ToString()}/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}temp/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}humidity/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    continue;
                }

                PublishThermostatState(thermostat);

                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/climate/{Global.mqtt_prefix}/thermostat{i.ToString()}/config",
                    JsonConvert.SerializeObject(thermostat.ToConfig(OmniLink.Controller.TempFormat)), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}temp/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigTemp(OmniLink.Controller.TempFormat)), MqttQualityOfServiceLevel.AtMostOnce, true);
                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}humidity/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigHumidity()), MqttQualityOfServiceLevel.AtMostOnce, true);
            }
        }

        private void PublishButtons()
        {
            log.Debug("Publishing buttons");

            for (ushort i = 1; i < OmniLink.Controller.Buttons.Count; i++)
            {
                clsButton button = OmniLink.Controller.Buttons[i];

                if (button.DefaultProperties == true)
                {
                    MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i.ToString()}/config", null, MqttQualityOfServiceLevel.AtMostOnce, true);
                    continue;
                }

                // Buttons are always off
                MqttClient.PublishAsync(button.ToTopic(Topic.state), "OFF", MqttQualityOfServiceLevel.AtMostOnce, true);

                MqttClient.PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i.ToString()}/config",
                    JsonConvert.SerializeObject(button.ToConfig()), MqttQualityOfServiceLevel.AtMostOnce, true);
            }
        }

        private void Omnilink_OnAreaStatus(object sender, AreaStatusEventArgs e)
        {
            PublishAreaState(e.Area);

            // Since the controller doesn't fire zone status change on area status change
            // request update so armed, tripped, and secure statuses are correct
            for (ushort i = 1; i < OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.Area == e.Area.Number)
                    OmniLink.Controller.Connection.Send(new clsOL2MsgRequestExtendedStatus(OmniLink.Controller.Connection, enuObjectType.Zone, i, i), HandleRequestZoneStatus);
            }
        }

        private void HandleRequestZoneStatus(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            clsOL2MsgExtendedStatus MSG = new clsOL2MsgExtendedStatus(OmniLink.Controller.Connection, B);

            for (byte i = 0; i < MSG.ZoneStatusCount(); i++)
            {
                clsZone zone = OmniLink.Controller.Zones[MSG.ObjectNumber(i)];
                zone.CopyExtendedStatus(MSG, i);
                MqttClient.PublishAsync(zone.ToTopic(Topic.state), zone.ToState(), MqttQualityOfServiceLevel.AtMostOnce, true);
            }
        }

        private void Omnilink_OnZoneStatus(object sender, ZoneStatusEventArgs e)
        {
            PublishZoneState(e.Zone);
        }

        private void Omnilink_OnUnitStatus(object sender, UnitStatusEventArgs e)
        {
            PublishUnitState(e.Unit);
        }

        private void Omnilink_OnThermostatStatus(object sender, ThermostatStatusEventArgs e)
        {
            // Ignore events fired by thermostat polling and when temperature is invalid
            // An invalid temperature can occur when a Zigbee thermostat is unreachable
            if(!e.EventTimer && e.Thermostat.Temp > 0)
                PublishThermostatState(e.Thermostat);
        }

        private void PublishAreaState(clsArea area)
        {
            MqttClient.PublishAsync(area.ToTopic(Topic.state), area.ToState(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(area.ToTopic(Topic.basic_state), area.ToBasicState(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(area.ToTopic(Topic.json_state), area.ToJsonState(), MqttQualityOfServiceLevel.AtMostOnce, true);
        }

        private void PublishZoneState(clsZone zone)
        {
            MqttClient.PublishAsync(zone.ToTopic(Topic.state), zone.ToState(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(zone.ToTopic(Topic.basic_state), zone.ToBasicState(), MqttQualityOfServiceLevel.AtMostOnce, true);

            if(zone.IsTemperatureZone())
                MqttClient.PublishAsync(zone.ToTopic(Topic.current_temperature), zone.TempText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            else if (zone.IsHumidityZone())
                MqttClient.PublishAsync(zone.ToTopic(Topic.current_humidity), zone.TempText(), MqttQualityOfServiceLevel.AtMostOnce, true);
        }

        private void PublishUnitState(clsUnit unit)
        {
            MqttClient.PublishAsync(unit.ToTopic(Topic.state), unit.ToState(), MqttQualityOfServiceLevel.AtMostOnce, true);

            if(unit.Number < 385)
                MqttClient.PublishAsync(unit.ToTopic(Topic.brightness_state), unit.ToBrightnessState().ToString(), MqttQualityOfServiceLevel.AtMostOnce, true);
        }

        private void PublishThermostatState(clsThermostat thermostat)
        {
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.current_operation), thermostat.ToOperationState(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.current_temperature), thermostat.TempText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.current_humidity), thermostat.HumidityText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.temperature_heat_state), thermostat.HeatSetpointText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.temperature_cool_state), thermostat.CoolSetpointText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.humidify_state), thermostat.HumidifySetpointText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.dehumidify_state), thermostat.DehumidifySetpointText(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.mode_state), thermostat.ModeText().ToLower(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.fan_mode_state), thermostat.FanModeText().ToLower(), MqttQualityOfServiceLevel.AtMostOnce, true);
            MqttClient.PublishAsync(thermostat.ToTopic(Topic.hold_state), thermostat.HoldStatusText().ToLower(), MqttQualityOfServiceLevel.AtMostOnce, true);
        }
    }
}
