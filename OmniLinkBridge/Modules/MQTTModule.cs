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
using OmniLinkBridge.MQTT.HomeAssistant;
using OmniLinkBridge.MQTT.Parser;
using OmniLinkBridge.OmniLink;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private Dictionary<string, int> AudioSources { get; set; } = new Dictionary<string, int>();

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        private const string ONLINE = "online";
        private const string OFFLINE = "offline";

        private const string SECURE = "secure";
        private const string TROUBLE = "trouble";

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
            OmniLink.OnLockStatus += OmniLink_OnLockStatus;
            OmniLink.OnAudioZoneStatus += OmniLink_OnAudioZoneStatus;
            OmniLink.OnSystemStatus += OmniLink_OnSystemStatus;

            MessageProcessor = new MessageProcessor(omni, AudioSources, omni.Controller.CAP.numAudioZones);
        }

        public void Startup()
        {
            MqttApplicationMessage lastwill = new MqttApplicationMessage()
            {
                Topic = $"{Global.mqtt_prefix}/status",
                Payload = Encoding.UTF8.GetBytes(OFFLINE),
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
                    sw_version = $"{OmniLink.Controller.GetVersionText()} - OmniLinkBridge {Assembly.GetExecutingAssembly().GetName().Version}",
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

            MqttClient.StartAsync(manoptions).Wait();

            MqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate((e) =>
                MessageProcessor.Process(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.Payload)));

            // Subscribe to notifications for these command topics
            List<Topic> toSubscribe = new List<Topic>()
            {
                Topic.command,
                Topic.alarm_command,
                Topic.brightness_command,
                Topic.flag_command,
                Topic.scene_command,
                Topic.temperature_heat_command,
                Topic.temperature_cool_command,
                Topic.humidify_command,
                Topic.dehumidify_command,
                Topic.mode_command,
                Topic.fan_mode_command,
                Topic.hold_command,
                Topic.mute_command,
                Topic.source_command,
                Topic.volume_command
            };

            toSubscribe.ForEach((command) => MqttClient.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{command}").Build()));

            // Wait until shutdown
            trigger.WaitOne();

            PublishControllerStatus(OFFLINE);

            MqttClient.StopAsync().Wait();
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
                PublishControllerStatus(OFFLINE);
        }

        private void PublishControllerStatus(string status)
        {
            log.Information("Publishing controller {status}", status);
            PublishAsync($"{Global.mqtt_prefix}/{Topic.status}", status);
        }

        private void PublishConfig()
        {
            PublishSystem();
            PublishAreas();
            PublishZones();
            PublishUnits();
            PublishThermostats();
            PublishButtons();
            PublishMessages();
            PublishLocks();
            PublishAudioSources();
            PublishAudioZones();

            PublishControllerStatus(ONLINE);
            PublishAsync($"{Global.mqtt_prefix}/model", OmniLink.Controller.GetModelText());
            PublishAsync($"{Global.mqtt_prefix}/version", OmniLink.Controller.GetVersionText());
        }

        private void PublishSystem()
        {
            log.Debug("Publishing {type}", "system");

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/system_phone/config",
                JsonConvert.SerializeObject(SystemTroubleConfig("phone", "Phone")));
            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/system_ac/config",
                JsonConvert.SerializeObject(SystemTroubleConfig("ac", "AC")));
            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/system_battery/config",
                JsonConvert.SerializeObject(SystemTroubleConfig("battery", "Battery")));
            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/system_dcm/config",
                JsonConvert.SerializeObject(SystemTroubleConfig("dcm", "DCM")));

            PublishAsync(SystemTroubleTopic("phone"), OmniLink.TroublePhone ? TROUBLE : SECURE);
            PublishAsync(SystemTroubleTopic("ac"), OmniLink.TroubleAC ? TROUBLE : SECURE);
            PublishAsync(SystemTroubleTopic("battery"), OmniLink.TroubleBattery ? TROUBLE : SECURE);
            PublishAsync(SystemTroubleTopic("dcm"), OmniLink.TroubleDCM ? TROUBLE : SECURE);
        }

        public string SystemTroubleTopic(string type)
        {
            return $"{Global.mqtt_prefix}/system/{type}/{Topic.state}";
        }

        public BinarySensor SystemTroubleConfig(string type, string name)
        {
            return new BinarySensor(MQTTModule.MqttDeviceRegistry)
            {
                unique_id = $"{Global.mqtt_prefix}system{type}",
                name = $"{Global.mqtt_discovery_name_prefix}System {name}",
                state_topic = SystemTroubleTopic(type),
                device_class = BinarySensor.DeviceClass.problem,
                payload_off = SECURE,
                payload_on = TROUBLE
            };
        }

        private void PublishAreas()
        {
            log.Debug("Publishing {type}", "areas");

            for (ushort i = 1; i <= OmniLink.Controller.Areas.Count; i++)
            {
                clsArea area = OmniLink.Controller.Areas[i];

                // PC Access doesn't let you customize the area name when configured for one area.
                // Ignore default properties for the first area.
                if (i > 1 && area.DefaultProperties == true)
                {
                    PublishAsync(area.ToTopic(Topic.name), null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/alarm_control_panel/{Global.mqtt_prefix}/area{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}burglary/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}fire/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}gas/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}aux/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}freeze/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}water/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}duress/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}temp/config", null);
                    continue;
                }

                PublishAreaState(area);

                PublishAsync(area.ToTopic(Topic.name), area.Name);
                PublishAsync($"{Global.mqtt_discovery_prefix}/alarm_control_panel/{Global.mqtt_prefix}/area{i}/config",
                    JsonConvert.SerializeObject(area.ToConfig()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}burglary/config",
                    JsonConvert.SerializeObject(area.ToConfigBurglary()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}fire/config",
                    JsonConvert.SerializeObject(area.ToConfigFire()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}gas/config",
                    JsonConvert.SerializeObject(area.ToConfigGas()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}aux/config",
                    JsonConvert.SerializeObject(area.ToConfigAux()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}freeze/config",
                    JsonConvert.SerializeObject(area.ToConfigFreeze()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}water/config",
                    JsonConvert.SerializeObject(area.ToConfigWater()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}duress/config",
                   JsonConvert.SerializeObject(area.ToConfigDuress()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/area{i}temp/config",
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
                    PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/zone{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/zone{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i}temp/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i}humidity/config", null);
                    continue;
                }

                PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{Global.mqtt_prefix}/zone{i}/config",
                    JsonConvert.SerializeObject(zone.ToConfig()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i}/config",
                    JsonConvert.SerializeObject(zone.ToConfigSensor()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/zone{i}/config",
                    JsonConvert.SerializeObject(zone.ToConfigSwitch()));

                if (zone.IsTemperatureZone())
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i}temp/config",
                        JsonConvert.SerializeObject(zone.ToConfigTemp(OmniLink.Controller.TempFormat)));
                else if (zone.IsHumidityZone())
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/zone{i}humidity/config",
                        JsonConvert.SerializeObject(zone.ToConfigHumidity()));
            }
        }

        private void PublishUnits()
        {
            log.Debug("Publishing {type}", "units");

            for (ushort i = 1; i <= OmniLink.Controller.Units.Count; i++)
            {
                clsUnit unit = OmniLink.Controller.Units[i];
                UnitType unitType = unit.ToUnitType();

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
                    foreach(UnitType entry in Enum.GetValues(typeof(UnitType)))
                        PublishAsync($"{Global.mqtt_discovery_prefix}/{entry}/{Global.mqtt_prefix}/unit{i}/config", null);

                    continue;
                }

                foreach (UnitType entry in Enum.GetValues(typeof(UnitType)).Cast<UnitType>().Where(x => x != unitType))
                        PublishAsync($"{Global.mqtt_discovery_prefix}/{entry}/{Global.mqtt_prefix}/unit{i}/config", null);

                log.Verbose("Publishing {type} {id} {name} as {unitType}", "units", i, unit.Name, unitType);

                if (unitType == UnitType.@switch)
                    PublishAsync($"{Global.mqtt_discovery_prefix}/{unitType}/{Global.mqtt_prefix}/unit{i}/config",
                        JsonConvert.SerializeObject(unit.ToConfigSwitch()));
                else if (unitType == UnitType.light)
                    PublishAsync($"{Global.mqtt_discovery_prefix}/{unitType}/{Global.mqtt_prefix}/unit{i}/config",
                        JsonConvert.SerializeObject(unit.ToConfig()));
                else if (unitType == UnitType.number)
                    PublishAsync($"{Global.mqtt_discovery_prefix}/{unitType}/{Global.mqtt_prefix}/unit{i}/config",
                        JsonConvert.SerializeObject(unit.ToConfigNumber()));
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
                    PublishAsync($"{Global.mqtt_discovery_prefix}/climate/{Global.mqtt_prefix}/thermostat{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/number/{Global.mqtt_prefix}/thermostat{i}humidify/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/number/{Global.mqtt_prefix}/thermostat{i}dehumidify/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i}temp/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i}humidity/config", null);
                    continue;
                }

                PublishThermostatState(thermostat);

                PublishAsync(thermostat.ToTopic(Topic.name), thermostat.Name);
                PublishAsync(thermostat.ToTopic(Topic.status), ONLINE);
                PublishAsync($"{Global.mqtt_discovery_prefix}/climate/{Global.mqtt_prefix}/thermostat{i}/config",
                    JsonConvert.SerializeObject(thermostat.ToConfig(OmniLink.Controller.TempFormat)));
                PublishAsync($"{Global.mqtt_discovery_prefix}/number/{Global.mqtt_prefix}/thermostat{i}humidify/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigHumidify()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/number/{Global.mqtt_prefix}/thermostat{i}dehumidify/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigDehumidify()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i}temp/config",
                    JsonConvert.SerializeObject(thermostat.ToConfigTemp(OmniLink.Controller.TempFormat)));
                PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{Global.mqtt_prefix}/thermostat{i}humidity/config",
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
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/button/{Global.mqtt_prefix}/button{i}/config", null);
                    continue;
                }

                // Buttons are off unless momentarily pressed
                PublishAsync(button.ToTopic(Topic.state), "OFF");

                PublishAsync(button.ToTopic(Topic.name), button.Name);

                if (Global.mqtt_discovery_button_type == typeof(Switch))
                {
                    log.Information("See {setting} for new option when publishing {type}", "mqtt_discovery_button_type", "buttons");

                    PublishAsync($"{Global.mqtt_discovery_prefix}/button/{Global.mqtt_prefix}/button{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i}/config",
                        JsonConvert.SerializeObject(button.ToConfigSwitch()));
                }
                else if (Global.mqtt_discovery_button_type == typeof(Button))
                {
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/button{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/button/{Global.mqtt_prefix}/button{i}/config",
                        JsonConvert.SerializeObject(button.ToConfigButton()));
                }
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

                PublishMessageStateAsync(message);

                PublishAsync(message.ToTopic(Topic.name), message.Name);
            }
        }

        private void PublishLocks()
        {
            log.Debug("Publishing {type}", "locks");

            for (ushort i = 1; i <= OmniLink.Controller.AccessControlReaders.Count; i++)
            {
                clsAccessControlReader reader = OmniLink.Controller.AccessControlReaders[i];

                if (reader.DefaultProperties == true)
                {
                    PublishAsync(reader.ToTopic(Topic.name), null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/lock/{Global.mqtt_prefix}/lock{i}/config", null);
                    continue;
                }

                PublishLockStateAsync(reader);

                PublishAsync(reader.ToTopic(Topic.name), reader.Name);
                PublishAsync($"{Global.mqtt_discovery_prefix}/lock/{Global.mqtt_prefix}/lock{i}/config",
                    JsonConvert.SerializeObject(reader.ToConfig()));
            }
        }

        private void PublishAudioSources()
        {
            log.Debug("Publishing {type}", "audio sources");

            for (ushort i = 1; i <= OmniLink.Controller.AudioSources.Count; i++)
            {
                clsAudioSource audioSource = OmniLink.Controller.AudioSources[i];

                if (audioSource.DefaultProperties == true)
                {
                    PublishAsync(audioSource.ToTopic(Topic.name), null);
                    continue;
                }

                PublishAsync(audioSource.ToTopic(Topic.name), audioSource.rawName);

                if (AudioSources.ContainsKey(audioSource.rawName))
                {
                    log.Warning("Duplicate audio source name {name}", audioSource.rawName);
                    continue;
                }

                AudioSources.Add(audioSource.rawName, i);
            }
        }

        private void PublishAudioZones()
        {
            log.Debug("Publishing {type}", "audio zones");

            for (ushort i = 1; i <= OmniLink.Controller.AudioZones.Count; i++)
            {
                clsAudioZone audioZone = OmniLink.Controller.AudioZones[i];

                if (audioZone.DefaultProperties == true)
                {
                    PublishAsync(audioZone.ToTopic(Topic.name), null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/audio{i}/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/audio{i}mute/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/select/{Global.mqtt_prefix}/audio{i}source/config", null);
                    PublishAsync($"{Global.mqtt_discovery_prefix}/number/{Global.mqtt_prefix}/audio{i}volume/config", null);
                    continue;
                }

                PublishAudioZoneStateAsync(audioZone);

                PublishAsync(audioZone.ToTopic(Topic.name), audioZone.rawName);
                PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/audio{i}/config",
                    JsonConvert.SerializeObject(audioZone.ToConfig()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/switch/{Global.mqtt_prefix}/audio{i}mute/config",
                    JsonConvert.SerializeObject(audioZone.ToConfigMute()));
                PublishAsync($"{Global.mqtt_discovery_prefix}/select/{Global.mqtt_prefix}/audio{i}source/config",
                    JsonConvert.SerializeObject(audioZone.ToConfigSource(new List<string>(AudioSources.Keys))));
                PublishAsync($"{Global.mqtt_discovery_prefix}/number/{Global.mqtt_prefix}/audio{i}volume/config",
                    JsonConvert.SerializeObject(audioZone.ToConfigVolume()));
            }

            PublishAsync($"{Global.mqtt_discovery_prefix}/button/{Global.mqtt_prefix}/audio0/config",
                JsonConvert.SerializeObject(new Button(MqttDeviceRegistry)
                {
                    unique_id = $"{Global.mqtt_prefix}audio0",
                    name = Global.mqtt_discovery_name_prefix + "Audio All Off",
                    icon = "mdi:speaker",
                    command_topic = $"{Global.mqtt_prefix}/audio0/{Topic.command}",
                    payload_press = "OFF"
                }));
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
            if (e.EventTimer)
                return;

            if (e.Offline)
            {
                PublishAsync(e.Thermostat.ToTopic(Topic.status), OFFLINE);
                return;
            }

            PublishAsync(e.Thermostat.ToTopic(Topic.status), ONLINE);
            PublishThermostatState(e.Thermostat);
        }

        private async void OmniLink_OnButtonStatus(object sender, ButtonStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            await PublishButtonStateAsync(e.Button);
        }

        private void OmniLink_OnMessageStatus(object sender, MessageStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishMessageStateAsync(e.Message);
        }

        private void OmniLink_OnLockStatus(object sender, LockStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishLockStateAsync(e.Reader);
        }

        private void OmniLink_OnAudioZoneStatus(object sender, AudioZoneStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            PublishAudioZoneStateAsync(e.AudioZone);
        }

        private void OmniLink_OnSystemStatus(object sender, SystemStatusEventArgs e)
        {
            if (!MqttClient.IsConnected)
                return;

            if(e.Type == SystemEventType.Phone)
                PublishAsync(SystemTroubleTopic("phone"), e.Trouble ? TROUBLE : SECURE);
            else if (e.Type == SystemEventType.AC)
                PublishAsync(SystemTroubleTopic("ac"), e.Trouble ? TROUBLE : SECURE);
            else if (e.Type == SystemEventType.Button)
                PublishAsync(SystemTroubleTopic("battery"), e.Trouble ? TROUBLE : SECURE);
            else if (e.Type == SystemEventType.DCM)
                PublishAsync(SystemTroubleTopic("dcm"), e.Trouble ? TROUBLE : SECURE);
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

            if (unit.Type == enuOL2UnitType.Flag)
            {
                PublishAsync(unit.ToTopic(Topic.flag_state), ((ushort)unit.Status).ToString());
            }
            else if(unit.Type != enuOL2UnitType.Output)
            {
                PublishAsync(unit.ToTopic(Topic.brightness_state), unit.ToBrightnessState().ToString());
                PublishAsync(unit.ToTopic(Topic.scene_state), unit.ToSceneState());
            }
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
            PublishAsync(thermostat.ToTopic(Topic.mode_state), thermostat.ToModeState());
            PublishAsync(thermostat.ToTopic(Topic.mode_basic_state), thermostat.ToModeBasicState());
            PublishAsync(thermostat.ToTopic(Topic.fan_mode_state), thermostat.FanModeText().ToLower());
            PublishAsync(thermostat.ToTopic(Topic.hold_state), thermostat.HoldStatusText().ToLower());
        }

        private async Task PublishButtonStateAsync(clsButton button)
        {
            // Simulate a momentary press
            await PublishAsync(button.ToTopic(Topic.state), "ON");
            await Task.Delay(1000);
            await PublishAsync(button.ToTopic(Topic.state), "OFF");
        }

        private Task PublishMessageStateAsync(clsMessage message)
        {
            return PublishAsync(message.ToTopic(Topic.state), message.ToState());
        }

        private Task PublishLockStateAsync(clsAccessControlReader reader)
        {
            return PublishAsync(reader.ToTopic(Topic.state), reader.ToState());
        }

        private void PublishAudioZoneStateAsync(clsAudioZone audioZone)
        {
            PublishAsync(audioZone.ToTopic(Topic.state), audioZone.ToState());
            PublishAsync(audioZone.ToTopic(Topic.mute_state), audioZone.ToMuteState());
            PublishAsync(audioZone.ToTopic(Topic.source_state), 
                OmniLink.Controller.AudioSources[audioZone.ToSourceState()].rawName);
            PublishAsync(audioZone.ToTopic(Topic.volume_state), audioZone.ToVolumeState().ToString());
        }

        private Task PublishAsync(string topic, string payload)
        {
            return MqttClient.PublishAsync(topic, payload, MqttQualityOfServiceLevel.AtMostOnce, true);
        }
    }
}
