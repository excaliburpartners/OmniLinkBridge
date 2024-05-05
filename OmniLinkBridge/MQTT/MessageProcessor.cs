using HAI_Shared;
using OmniLinkBridge.MQTT.Parser;
using OmniLinkBridge.OmniLink;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace OmniLinkBridge.MQTT
{
    public class MessageProcessor
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Regex regexTopic = new Regex(Global.mqtt_prefix + "/([A-Za-z]+)([0-9]+)/(.*)", RegexOptions.Compiled);

        private readonly int[] audioMuteVolumes;
        private const int VOLUME_DEFAULT = 10;

        private IOmniLinkII OmniLink { get; }
        private Dictionary<string, int> AudioSources { get; }

        public MessageProcessor(IOmniLinkII omni, Dictionary<string, int> audioSources, int numAudioZones)
        {
            OmniLink = omni;
            AudioSources = audioSources;

            audioMuteVolumes = new int[numAudioZones];
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
            else if (type == CommandTypes.zone && id <= OmniLink.Controller.Zones.Count)
                ProcessZoneReceived(OmniLink.Controller.Zones[id], topic, payload);
            else if (type == CommandTypes.unit && id > 0 && id <= OmniLink.Controller.Units.Count)
                ProcessUnitReceived(OmniLink.Controller.Units[id], topic, payload);
            else if (type == CommandTypes.thermostat && id > 0 && id <= OmniLink.Controller.Thermostats.Count)
                ProcessThermostatReceived(OmniLink.Controller.Thermostats[id], topic, payload);
            else if (type == CommandTypes.button && id > 0 && id <= OmniLink.Controller.Buttons.Count)
                ProcessButtonReceived(OmniLink.Controller.Buttons[id], topic, payload);
            else if (type == CommandTypes.message && id > 0 && id <= OmniLink.Controller.Messages.Count)
                ProcessMessageReceived(OmniLink.Controller.Messages[id], topic, payload);
            else if (type == CommandTypes.@lock && id <= OmniLink.Controller.AccessControlReaders.Count)
                ProcessLockReceived(OmniLink.Controller.AccessControlReaders[id], topic, payload);
            else if (type == CommandTypes.audio && id <= OmniLink.Controller.AudioZones.Count)
                ProcessAudioReceived(OmniLink.Controller.AudioZones[id], topic, payload);
        }

        private static readonly IDictionary<AreaCommands, enuUnitCommand> AreaMapping = new Dictionary<AreaCommands, enuUnitCommand>
        {
            { AreaCommands.disarm, enuUnitCommand.SecurityOff },
            { AreaCommands.arm_home, enuUnitCommand.SecurityDay },
            { AreaCommands.arm_away, enuUnitCommand.SecurityAway },
            { AreaCommands.arm_night, enuUnitCommand.SecurityNight },
            { AreaCommands.arm_vacation, enuUnitCommand.SecurityVac },
            // The below aren't supported by Home Assistant
            { AreaCommands.arm_home_instant, enuUnitCommand.SecurityDyi },
            { AreaCommands.arm_night_delay, enuUnitCommand.SecurityNtd }
        };

        private void ProcessAreaReceived(clsArea area, Topic command, string payload)
        {
            AreaCommandCode parser = payload.ToCommandCode(supportValidate: true);

            if (parser.Success && command == Topic.command && Enum.TryParse(parser.Command, true, out AreaCommands cmd))
            {
                if (area.Number == 0)
                    log.Debug("SetArea: 0 implies all areas will be changed");

                if (parser.Validate)
                {
                    string sCode = parser.Code.ToString();

                    if (sCode.Length != 4)
                    {
                        log.Warning("SetArea: {id}, Invalid security code: must be 4 digits", area.Number);
                        return;
                    }

                    OmniLink.Controller.Connection.Send(new clsOL2MsgRequestValidateCode(OmniLink.Controller.Connection)
                    {
                        Area = (byte)area.Number,
                        Digit1 = (byte)int.Parse(sCode[0].ToString()),
                        Digit2 = (byte)int.Parse(sCode[1].ToString()),
                        Digit3 = (byte)int.Parse(sCode[2].ToString()),
                        Digit4 = (byte)int.Parse(sCode[3].ToString())
                    }, (M, B, Timeout) =>
                    {
                        if (Timeout || !((B.Length > 3) && (B[0] == 0x21) && (enuOmniLink2MessageType)B[2] == enuOmniLink2MessageType.ValidateCode))
                            return;

                        var validateCode = new clsOL2MsgValidateCode(OmniLink.Controller.Connection, B);

                        if (validateCode.AuthorityLevel == 0)
                        {
                            log.Warning("SetArea: {id}, Invalid security code: validation failed", area.Number);
                            return;
                        }

                        log.Debug("SetArea: {id}, Validated security code, Code Number: {code}, Authority: {authority}",
                            area.Number, validateCode.CodeNumber, validateCode.AuthorityLevel.ToString());

                        log.Debug("SetArea: {id} to {value}, Code Number: {code}",
                            area.Number, cmd.ToString().Replace("arm_", "").Replace("_", " "), validateCode.CodeNumber);

                        OmniLink.SendCommand(AreaMapping[cmd], validateCode.CodeNumber, (ushort)area.Number);
                    });

                    return;
                }

                log.Debug("SetArea: {id} to {value}, Code Number: {code}",
                    area.Number, cmd.ToString().Replace("arm_", "").Replace("_", " "), parser.Code);

                OmniLink.SendCommand(AreaMapping[cmd], (byte)parser.Code, (ushort)area.Number);
            }
            else if (command == Topic.alarm_command && area.Number > 0 && Enum.TryParse(parser.Command, true, out AlarmCommands alarm))
            {
                log.Debug("SetAreaAlarm: {id} to {value}", area.Number, parser.Command);

                OmniLink.Controller.Connection.Send(new clsOL2MsgActivateKeypadEmg(OmniLink.Controller.Connection)
                {
                    Area = (byte)area.Number,
                    EmgType = (byte)alarm
                }, (M, B, Timeout) => { });
            }
        }

        private static readonly IDictionary<ZoneCommands, enuUnitCommand> ZoneMapping = new Dictionary<ZoneCommands, enuUnitCommand>
        {
            { ZoneCommands.restore, enuUnitCommand.Restore },
            { ZoneCommands.bypass, enuUnitCommand.Bypass },
        };

        private void ProcessZoneReceived(clsZone zone, Topic command, string payload)
        {
            AreaCommandCode parser = payload.ToCommandCode();

            if (parser.Success && command == Topic.command && Enum.TryParse(parser.Command, true, out ZoneCommands cmd) &&
                !(zone.Number == 0 && cmd == ZoneCommands.bypass))
            {
                if (zone.Number == 0)
                    log.Debug("SetZone: 0 implies all zones will be restored");

                log.Debug("SetZone: {id} to {value}", zone.Number, parser.Command);
                OmniLink.SendCommand(ZoneMapping[cmd], (byte)parser.Code, (ushort)zone.Number);
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
            else if (unit.Type == enuOL2UnitType.Flag &&
                command == Topic.flag_command && int.TryParse(payload, out int flagValue))
            {
                log.Debug("SetUnit: {id} to {value}", unit.Number, payload);
                OmniLink.SendCommand(enuUnitCommand.Set, BitConverter.GetBytes(flagValue)[0], (ushort)unit.Number);
            }
            else if (unit.Type != enuOL2UnitType.Output &&
                command == Topic.brightness_command && int.TryParse(payload, out int unitValue))
            {
                log.Debug("SetUnit: {id} to {value}%", unit.Number, payload);
                OmniLink.SendCommand(enuUnitCommand.Level, BitConverter.GetBytes(unitValue)[0], (ushort)unit.Number);

                // Force status change instead of waiting on controller to update
                // Home Assistant sends brightness immediately followed by ON,
                // which will cause light to go to 100% brightness
                unit.Status = (byte)(100 + unitValue);
            }
            else if (unit.Type != enuOL2UnitType.Output &&
                command == Topic.scene_command && char.TryParse(payload, out char scene))
            {
                log.Debug("SetUnit: {id} to {value}", unit.Number, payload);
                OmniLink.SendCommand(enuUnitCommand.Compose, (byte)(scene - 63), (ushort)unit.Number);
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
                if (thermostat.Type == enuThermostatType.AutoHeatCool ||
                    (thermostat.Type == enuThermostatType.HeatCool && mode != enuThermostatMode.Auto) ||
                    (thermostat.Type == enuThermostatType.CoolOnly &&
                        (mode == enuThermostatMode.Off || mode == enuThermostatMode.Cool)) ||
                    (thermostat.Type == enuThermostatType.HeatOnly &&
                        (mode == enuThermostatMode.Off || mode == enuThermostatMode.Heat || mode == enuThermostatMode.E_Heat)) ||
                    mode == enuThermostatMode.Off)
                {
                    log.Debug("SetThermostatMode: {id} to {value}", thermostat.Number, payload);
                    OmniLink.SendCommand(enuUnitCommand.Mode, BitConverter.GetBytes((int)mode)[0], (ushort)thermostat.Number);
                }
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

        private static readonly IDictionary<LockCommands, enuUnitCommand> LockMapping = new Dictionary<LockCommands, enuUnitCommand>
        {
            { LockCommands.@lock, enuUnitCommand.Lock },
            { LockCommands.unlock, enuUnitCommand.Unlock },
        };

        private void ProcessLockReceived(clsAccessControlReader reader, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out LockCommands cmd))
            {
                if (reader.Number == 0)
                    log.Debug("SetLock: 0 implies all locks will be changed");

                log.Debug("SetLock: {id} to {value}", reader.Number, payload);

                OmniLink.SendCommand(LockMapping[cmd], 0, (ushort)reader.Number);
            }
        }

        private void ProcessAudioReceived(clsAudioZone audioZone, Topic command, string payload)
        {
            if (command == Topic.command && Enum.TryParse(payload, true, out UnitCommands cmd))
            {
                if (audioZone.Number == 0)
                    log.Debug("SetAudio: 0 implies all audio zones will be changed");

                log.Debug("SetAudio: {id} to {value}", audioZone.Number, payload);

                OmniLink.SendCommand(enuUnitCommand.AudioZone, (byte)cmd, (ushort)audioZone.Number);

                // Send power ON twice to workaround Russound standby
                if(cmd == UnitCommands.ON)
                {
                    Thread.Sleep(500);
                    OmniLink.SendCommand(enuUnitCommand.AudioZone, (byte)cmd, (ushort)audioZone.Number);
                }
            }
            else if (command == Topic.mute_command && Enum.TryParse(payload, true, out UnitCommands mute))
            {
                if (audioZone.Number == 0)
                {
                    if (Global.mqtt_audio_local_mute)
                    {
                        log.Warning("SetAudioMute: 0 not supported with local mute");
                        return;
                    }
                    else
                        log.Debug("SetAudioMute: 0 implies all audio zones will be changed");
                }

                if (Global.mqtt_audio_local_mute)
                {
                    if (mute == UnitCommands.ON)
                    {
                        log.Debug("SetAudioMute: {id} local mute, previous volume {level}",
                            audioZone.Number, audioZone.Volume);
                        audioMuteVolumes[audioZone.Number] = audioZone.Volume;

                        OmniLink.SendCommand(enuUnitCommand.AudioVolume, 0, (ushort)audioZone.Number);
                    }
                    else
                    {
                        if (audioMuteVolumes[audioZone.Number] == 0)
                        {
                            log.Debug("SetAudioMute: {id} local mute, defaulting to volume {level}",
                                audioZone.Number, VOLUME_DEFAULT);
                            audioMuteVolumes[audioZone.Number] = VOLUME_DEFAULT;
                        }
                        else
                        {
                            log.Debug("SetAudioMute: {id} local mute, restoring to volume {level}",
                                audioZone.Number, audioMuteVolumes[audioZone.Number]);
                        }

                        OmniLink.SendCommand(enuUnitCommand.AudioVolume, (byte)audioMuteVolumes[audioZone.Number], (ushort)audioZone.Number);
                    }
                }
                else
                {
                    log.Debug("SetAudioMute: {id} to {value}", audioZone.Number, payload);

                    OmniLink.SendCommand(enuUnitCommand.AudioZone, (byte)(mute + 2), (ushort)audioZone.Number);
                }
            }
            else if (command == Topic.source_command && AudioSources.TryGetValue(payload, out int source))
            {
                log.Debug("SetAudioSource: {id} to {value}", audioZone.Number, payload);

                OmniLink.SendCommand(enuUnitCommand.AudioSource, (byte)source, (ushort)audioZone.Number);
            }
            else if (command == Topic.volume_command && double.TryParse(payload, out double volume))
            {
                if (Global.mqtt_audio_volume_media_player)
                    volume *= 100;

                if (volume > 100)
                    volume = 100;
                else if (volume < 0)
                    volume = 0;

                log.Debug("SetAudioVolume: {id} to {value}", audioZone.Number, volume);

                OmniLink.SendCommand(enuUnitCommand.AudioVolume, (byte)volume, (ushort)audioZone.Number);
            }
        }
    }
}
