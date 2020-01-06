using HAI_Shared;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using OmniLinkBridge.MQTT;
using OmniLinkBridge.OmniLink;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniLinkBridge.Modules
{
    public class MQTTModule : IModule
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        public static DeviceRegistry MqttDeviceRegistry { get; set; }

        private OmniLinkII OmniLink { get; set; }
        private IManagedMqttClient MqttClient { get; set; }
        private bool ControllerConnected { get; set; }
        private MessageProcessor MessageProcessor { get; set; }

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        public MQTTModule(OmniLinkII omni)
        {
            OmniLink = omni;
            OmniLink.OnConnect += OmniLink_OnConnect;
            OmniLink.OnDisconnect += OmniLink_OnDisconnect;
            OmniLink.OnAreaStatus += Omnilink_OnAreaStatus;
            OmniLink.OnZoneStatus += Omnilink_OnZoneStatus;
            OmniLink.OnUnitStatus += Omnilink_OnUnitStatus;
            OmniLink.OnThermostatStatus += Omnilink_OnThermostatStatus;
            OmniLink.OnButtonStatus += OmniLink_OnButtonStatus;
            OmniLink.OnMessageStatus += OmniLink_OnMessageStatus;

            MessageProcessor = new MessageProcessor(omni);
        }

        public void Startup()
        {
            MqttApplicationMessage lastwill = new MqttApplicationMessage()
            {
                Topic = $"{Global.mqtt_prefix}/status",
                Payload = Encoding.UTF8.GetBytes("offline"),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                Retain = true
            };

            MqttClientOptionsBuilder options = new MqttClientOptionsBuilder()
                .WithTcpServer(Global.mqtt_server)
                .WithWillMessage(lastwill);

            if (!string.IsNullOrEmpty(Global.mqtt_username))
                options = options
                    .WithCredentials(Global.mqtt_username, Global.mqtt_password);

            ManagedMqttClientOptions manoptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(options.Build())
                .Build();

            MqttClient = new MqttFactory().CreateManagedMqttClient();
            MqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate((e) =>
            {
                log.Debug("Connected");

                MqttDeviceRegistry = new DeviceRegistry()
                {
                    identifiers = Global.mqtt_prefix,
                    name = Global.mqtt_prefix,
                    sw_version = $"{OmniLink.Controller.GetVersionText()} - OmniLinkBridge {Assembly.GetExecutingAssembly().GetName().Version.ToString()}",
                    model = OmniLink.Controller.GetModelText(),
                    manufacturer = "Leviton"
                };

                // For the initial connection wait for the controller connected event to publish config
                // For subsequent connections publish config immediately
                if (ControllerConnected)
                    PublishConfig();
            });
            MqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate((e) => log.Error("Error connecting {reason}", e.Exception.Message));
            MqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate((e) => log.Debug("Disconnected"));

            MqttClient.StartAsync(manoptions);

            MqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate((e) =>
                MessageProcessor.Process(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.Payload)));

            // Subscribe to notifications for these command topics
            List<Topic> toSubscribe = new List<Topic>()
            {
                Topic.command,
                Topic.brightness_command,
                Topic.temperature_heat_command,
                Topic.temperature_cool_command,
                Topic.humidify_command,
                Topic.dehumidify_command,
                Topic.mode_command,
                Topic.fan_mode_command,
                Topic.hold_command
            };

            toSubscribe.ForEach((command) => MqttClient.SubscribeAsync(
                new TopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{command.ToString()}").Build()));

            // Wait until shutdown
            trigger.WaitOne();

            PublishControllerStatus("offline");

            MqttClient.StopAsync();
        }

        public void Shutdown()
        {
            trigger.Set();
        }

        private void OmniLink_OnConnect(object sender, EventArgs e)
        {
            if(MqttClient.IsConnected)
                PublishConfig();

            ControllerConnected = true;
        }

        private void OmniLink_OnDisconnect(object sender, EventArgs e)
        {
            ControllerConnected = false;

            if (MqttClient.IsConnected)
                PublishControllerStatus("offline");
        }

        private void PublishControllerStatus(string status)
        {
            log.Information("Publishing controller {status}", status);
            PublishAsync($"{Global.mqtt_prefix}/status", status);
        }

        private void PublishConfig()
        {
            PublishAreas();
            PublishZones();
            PublishUnits();
            PublishThermostats();
            PublishButtons();
            PublishMessages();

            PublishControllerStatus("online");
            PublishAsync($"{Global.mqtt_prefix}/model", OmniLink.Controller.GetModelText());
            PublishAsync($"{Global.mqtt_prefix}/version", OmniLink.Controller.GetVersionText());
        }

        private void PublishAreas()
        {
            log.Debug("Publishing {type}", "areas");

            for (ushort i = 1; i <= OmniLink.Controller.Areas.Count; i++)
            {
                clsArea area = OmniLink.Controller.Areas[i];

                // PC Access doesn't let you customize the area name for the Omni LTe or Omni IIe
                // (configured for 1 area). To workaround ignore default properties for the first area.
                if (i > 1 && area.DefaultProperties == true)
                {
                    PublishAsync(area.ToTopic(Topic.name), null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/alarm_control_panel/{Global.mqtt_prefix}/area{i.ToString()}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}burglary/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}fire/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}gas/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}aux/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}freeze/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}water/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}duress/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}temp/config", null);
                    continue;
                }

                PublishAreaState(area);

                PublishAsync(area.ToTopic(Topic.name), area.Name);
                PublishAsync($"{Global.mqtt_discovery_prefix}/alarm_control_panel/{Global.mqtt_prefix}/area{i.ToString()}/config",
                    JsonConvert.SerializeObject(area.ToConfig()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}burglary/config",
                    JsonConvert.SerializeObject(area.ToConfigBurglary()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}fire/config",
                    JsonConvert.SerializeObject(area.ToConfigFire()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}gas/config",
                    JsonConvert.SerializeObject(area.ToConfigGas()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}aux/config",
                    JsonConvert.SerializeObject(area.ToConfigAux()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}freeze/config",
                    JsonConvert.SerializeObject(area.ToConfigFreeze()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}water/config",
                    JsonConvert.SerializeObject(area.ToConfigWater()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}duress/config",
                   JsonConvert.SerializeObject(area.ToConfigDuress()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i.ToString()}temp/config",
                   JsonConvert.SerializeObject(area.ToConfigTemp()));
            }
        }

        private void PublishZones()
        {
            log.Debug("Publishing {type}", "zones");

            for (ushort i = 1; i <= OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == true)
                {
                    PublishAsync(zone.ToTopic(Topic.name), null);
                }
                else
                {
                    PublishZoneState(zone);
                    PublishAsync(zone.ToTopic(Topic.name), zone.Name);
                }

                if (zone.DefaultProperties == true || Global.mqtt_discovery_ignore_zones.Contains(zone.Number))
                {                  
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}temp/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}humidity/config", null);
                    continue;
                }

                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config",
                    JsonConvert.SerializeObject(zone.ToConfig()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}/config",
                    JsonConvert.SerializeObject(zone.ToConfigSensor()));

                if (zone.IsTemperatureZone())
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}temp/config",
                        JsonConvert.SerializeObject(zone.ToConfigTemp(OmniLink.Controller.TempFormat)));
                else if (zone.IsHumidityZone())
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i.ToString()}humidity/config",
                        JsonConvert.SerializeObject(zone.ToConfigHumidity()));
            }
        }

        private void PublishUnits()
        {
            log.Debug("Publishing {type}", "units");

            for (ushort i = 1; i <= OmniLink.Controller.Units.Count; i++)
            {
                clsUnit unit = OmniLink.Controller.Units[i];

                if (unit.DefaultProperties == true)
                {
                    PublishAsync(unit.ToTopic(Topic.name), null);
                }
                else
                {
                    PublishUnitState(unit);
                    PublishAsync(unit.ToTopic(Topic.name), unit.Name);  
                }

                if (unit.DefaultProperties == true || Global.mqtt_discovery_ignore_units.Contains(unit.Number))
                {
                    string type = i < 385 ? "light" : "switch";
                    PublishAsync($"{Global.mqtt_discovery_prefix}/{type}/{Global.mqtt_prefix}/unit{i.ToString()}/config", null);
                    continue;
                }

                if (i < 385)
                    PublishAsync($"{Global.mqtt_discovery_prefix}/light/{Global.mqtt_prefix}/unit{i.ToString()}/config",
                        JsonConvert.SerializeObject(unit.ToConfig()));
                else
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/unit{i.ToString()}/config",
                        JsonConvert.SerializeObject(unit.ToConfigSwitch()));
            }
        }

        private void PublishThermostats()
        {
            log.Debug("Publishing {type}", "thermostats");

            for (ushort i = 1; i <= OmniLink.Controller.Thermostats.Count; i++)
            {
                clsThermostat thermostat = OmniLink.Controller.Thermostats[i];

                if (thermostat.DefaultProperties == true)
                {
                    PublishAsync(thermostat.ToTopic(Topic.name), null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/climate/{Global.mqtt_prefix}/thermostat{i.ToString()}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}temp/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}humidity/config", null);
                    continue;
                }

                PublishThermostatState(thermostat);

                PublishAsync(thermostat.ToTopic(Topic.name), thermostat.Name);
                PublishAsync($"{Global.mqtt_discovery_prefix}/climate/{Global.mqtt_prefix}/thermostat{i.ToString()}/config",
                    JsonConvert.SerializeObject(thermostat.ToConfig(OmniLink.Controller.TempFormat)));
                PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}temp/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigTemp(OmniLink.Controller.TempFormat)));
                PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i.ToString()}humidity/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigHumidity()));
            }
        }

        private void PublishButtons()
        {
            log.Debug("Publishing {type}", "buttons");

            for (ushort i = 1; i <= OmniLink.Controller.Buttons.Count; i++)
            {
                clsButton button = OmniLink.Controller.Buttons[i];

                if (button.DefaultProperties == true)
                {
                    PublishAsync(button.ToTopic(Topic.name), null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i.ToString()}/config", null);
                    continue;
                }

                // Buttons are off unless momentarily pressed
                PublishAsync(button.ToTopic(Topic.state), "OFF");

                PublishAsync(button.ToTopic(Topic.name), button.Name);
                PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i.ToString()}/config",
                    JsonConvert.SerializeObject(button.ToConfig()));
            }
        }

        private void PublishMessages()
        {
            log.Debug("Publishing {type}", "messages");

            for (ushort i = 1; i <= OmniLink.Controller.Messages.Count; i++)
            {
                clsMessage message = OmniLink.Controller.Messages[i];

                if (message.DefaultProperties == true)
                {
                    PublishAsync(message.ToTopic(Topic.name), null);
                    continue;
                }

                PublishMessageState(message);

                PublishAsync(message.ToTopic(Topic.name), message.Name);
            }
        }

        private void Omnilink_OnAreaStatus(object sender, AreaStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishAreaState(e.Area);

            // Since the controller doesn't fire zone status change on area status change
            // request update so armed, tripped, and secure statuses are correct
            for (ushort i = 1; i <= OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.Area == e.Area.Number)
                    OmniLink.Controller.Connection.Send(new clsOL2MsgRequestExtendedStatus(
                        OmniLink.Controller.Connection, enuObjectType.Zone, i, i), HandleRequestZoneStatus);
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
                PublishAsync(zone.ToTopic(Topic.state), zone.ToState());
            }
        }

        private void Omnilink_OnZoneStatus(object sender, ZoneStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishZoneState(e.Zone);
        }

        private void Omnilink_OnUnitStatus(object sender, UnitStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishUnitState(e.Unit);
        }

        private void Omnilink_OnThermostatStatus(object sender, ThermostatStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            // Ignore events fired by thermostat polling
            if (!e.EventTimer)
                PublishThermostatState(e.Thermostat);
        }

        private async void OmniLink_OnButtonStatus(object sender, ButtonStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            await PublishButtonState(e.Button);
        }

        private void OmniLink_OnMessageStatus(object sender, MessageStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishMessageState(e.Message);
        }

        private void PublishAreaState(clsArea area)
        {
            PublishAsync(area.ToTopic(Topic.state), area.ToState());
            PublishAsync(area.ToTopic(Topic.basic_state), area.ToBasicState());
            PublishAsync(area.ToTopic(Topic.json_state), area.ToJsonState());
        }

        private void PublishZoneState(clsZone zone)
        {
            PublishAsync(zone.ToTopic(Topic.state), zone.ToState());
            PublishAsync(zone.ToTopic(Topic.basic_state), zone.ToBasicState());

            if(zone.IsTemperatureZone())
                PublishAsync(zone.ToTopic(Topic.current_temperature), zone.TempText());
            else if (zone.IsHumidityZone())
                PublishAsync(zone.ToTopic(Topic.current_humidity), zone.TempText());
        }

        private void PublishUnitState(clsUnit unit)
        {
            PublishAsync(unit.ToTopic(Topic.state), unit.ToState());

            if(unit.Number < 385)
                PublishAsync(unit.ToTopic(Topic.brightness_state), unit.ToBrightnessState().ToString());
        }

        private void PublishThermostatState(clsThermostat thermostat)
        {
            PublishAsync(thermostat.ToTopic(Topic.current_operation), thermostat.ToOperationState());
            PublishAsync(thermostat.ToTopic(Topic.current_temperature), thermostat.TempText());
            PublishAsync(thermostat.ToTopic(Topic.current_humidity), thermostat.HumidityText());
            PublishAsync(thermostat.ToTopic(Topic.temperature_heat_state), thermostat.HeatSetpointText());
            PublishAsync(thermostat.ToTopic(Topic.temperature_cool_state), thermostat.CoolSetpointText());
            PublishAsync(thermostat.ToTopic(Topic.humidify_state), thermostat.HumidifySetpointText());
            PublishAsync(thermostat.ToTopic(Topic.dehumidify_state), thermostat.DehumidifySetpointText());
            PublishAsync(thermostat.ToTopic(Topic.mode_state), thermostat.ModeText().ToLower());
            PublishAsync(thermostat.ToTopic(Topic.fan_mode_state), thermostat.FanModeText().ToLower());
            PublishAsync(thermostat.ToTopic(Topic.hold_state), thermostat.HoldStatusText().ToLower());
        }

        private async Task PublishButtonState(clsButton button)
        {
            // Simulate a momentary press
            await PublishAsync(button.ToTopic(Topic.state), "ON");
            await Task.Delay(1000);
            await PublishAsync(button.ToTopic(Topic.state), "OFF");
        }

        private void PublishMessageState(clsMessage message)
        {
            PublishAsync(message.ToTopic(Topic.state), message.ToState());
        }

        private Task PublishAsync(string topic, string payload)
        {
            return MqttClient.PublishAsync(topic, payload, MqttQualityOfServiceLevel.AtMostOnce, true);
        }
    }
}
