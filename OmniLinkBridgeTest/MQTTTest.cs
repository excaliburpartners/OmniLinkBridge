using HAI_Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge;
using OmniLinkBridge.MQTT;
using OmniLinkBridgeTest.Mock;
using System.Collections.Concurrent;

namespace OmniLinkBridgeTest
{
    [TestClass]
    public class MQTTTest
    {
        MockOmniLinkII omniLink;
        MessageProcessor messageProcessor;

        [TestInitialize]
        public void Initialize()
        {
            omniLink = new MockOmniLinkII();
            messageProcessor = new MessageProcessor(omniLink);

            omniLink.Controller.Units[395].Type = enuOL2UnitType.Flag;
        }

        [TestMethod]
        public void AreaCommandInvalid()
        {
            SendCommandEventArgs actual = null;
            omniLink.OnSendCommand += (sender, e) => { actual = e; };

            // Invalid command
            messageProcessor.Process($"omnilink/area1/command", "disarmed");
            Assert.IsNull(actual);

            // Out of range
            messageProcessor.Process($"omnilink/area9/command", "disarm");
            Assert.IsNull(actual);
        }

        [TestMethod]
        public void AreaCommand()
        {
            void check(ushort id, int code, string payload, enuUnitCommand command)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/area{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = (byte)code,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            // Standard format
            check(1, 0, "disarm", enuUnitCommand.SecurityOff);
            check(1, 0, "arm_home", enuUnitCommand.SecurityDay);
            check(1, 0, "arm_away", enuUnitCommand.SecurityAway);
            check(1, 0, "arm_night", enuUnitCommand.SecurityNight);
            check(1, 0, "arm_home_instant", enuUnitCommand.SecurityDyi);
            check(1, 0, "arm_night_delay", enuUnitCommand.SecurityNtd);
            check(1, 0, "arm_vacation", enuUnitCommand.SecurityVac);

            // Check all areas
            check(0, 0, "disarm", enuUnitCommand.SecurityOff);

            // Check with optional code
            check(1, 1, "disarm,1", enuUnitCommand.SecurityOff);

            // Check case insensitivity
            check(8, 0, "DISARM", enuUnitCommand.SecurityOff);
        }

        [TestMethod]
        public void ZoneCommand()
        {
            void check(ushort id, int code, string payload, enuUnitCommand command, bool ensureNull = false)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/zone{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = (byte)code,
                    Pr2 = id
                };

                if (ensureNull)
                    Assert.IsNull(actual);
                else
                    Assert.AreEqual(expected, actual);
            }

            // Standard format
            check(1, 0, "bypass", enuUnitCommand.Bypass);
            check(1, 0, "restore", enuUnitCommand.Restore);

            // Check all zones
            check(0, 0, "restore", enuUnitCommand.Restore);

            // Not allowed to bypass all zones
            check(0, 0, "bypass", enuUnitCommand.Bypass, true);

            // Check with optional code
            check(1, 1, "bypass,1", enuUnitCommand.Bypass);

            // Check case insensitivity
            check(2, 0, "BYPASS", enuUnitCommand.Bypass);
        }

        [TestMethod]
        public void UnitCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/unit{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = 0,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(1, "ON", enuUnitCommand.On);
            omniLink.Controller.Units[1].Status = 1;
            check(1, "OFF", enuUnitCommand.Off);

            check(2, "on", enuUnitCommand.On);
        }

        [TestMethod]
        public void UnitFlagCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command, int value)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/unit{id}/flag_command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = (byte)value,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(395, "0", enuUnitCommand.Set, 0);
            check(395, "1", enuUnitCommand.Set, 1);
            check(395, "255", enuUnitCommand.Set, 255);
        }

        [TestMethod]
        public void UnitLevelCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command, int level)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/unit{id}/brightness_command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = (byte)level,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(1, "50", enuUnitCommand.Level, 50);
        }

        [TestMethod]
        public void ThermostatModeCommandInvalid()
        {
            SendCommandEventArgs actual = null;
            omniLink.OnSendCommand += (sender, e) => { actual = e; };

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.HeatCool;
            messageProcessor.Process($"omnilink/thermostat1/mode_command", "auto");
            Assert.IsNull(actual);

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.CoolOnly;
            messageProcessor.Process($"omnilink/thermostat1/mode_command", "auto");
            Assert.IsNull(actual);
            messageProcessor.Process($"omnilink/thermostat1/mode_command", "heat");
            Assert.IsNull(actual);

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.HeatOnly;
            messageProcessor.Process($"omnilink/thermostat1/mode_command", "auto");
            Assert.IsNull(actual);
            messageProcessor.Process($"omnilink/thermostat1/mode_command", "cool");
            Assert.IsNull(actual);
        }

        [TestMethod]
        public void ThermostatModeCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command, int mode)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/thermostat{id}/mode_command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = (byte)mode,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.AutoHeatCool;

            check(1, "auto", enuUnitCommand.Mode, (int)enuThermostatMode.Auto);
            check(1, "cool", enuUnitCommand.Mode, (int)enuThermostatMode.Cool);
            check(1, "heat", enuUnitCommand.Mode, (int)enuThermostatMode.Heat);
            check(1, "off", enuUnitCommand.Mode, (int)enuThermostatMode.Off);

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.HeatCool;

            check(1, "cool", enuUnitCommand.Mode, (int)enuThermostatMode.Cool);
            check(1, "heat", enuUnitCommand.Mode, (int)enuThermostatMode.Heat);
            check(1, "off", enuUnitCommand.Mode, (int)enuThermostatMode.Off);

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.CoolOnly;

            check(1, "cool", enuUnitCommand.Mode, (int)enuThermostatMode.Cool);
            check(1, "off", enuUnitCommand.Mode, (int)enuThermostatMode.Off);

            omniLink.Controller.Thermostats[1].Type = enuThermostatType.HeatOnly;

            check(1, "heat", enuUnitCommand.Mode, (int)enuThermostatMode.Heat);
            check(1, "off", enuUnitCommand.Mode, (int)enuThermostatMode.Off);
        }

        [TestMethod]
        public void ButtonCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/button{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = 0,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(1, "ON", enuUnitCommand.Button);
            check(1, "on", enuUnitCommand.Button);
        }

        [TestMethod]
        public void MessageCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command, byte par)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/message{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = par,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(1, "show", enuUnitCommand.ShowMsgWBeep, 0);
            check(1, "show_no_beep", enuUnitCommand.ShowMsgNoBeep, 1);
            check(1, "show_no_beep_or_led", enuUnitCommand.ShowMsgNoBeep, 2);
            check(1, "clear", enuUnitCommand.ClearMsg, 0);

            check(2, "SHOW", enuUnitCommand.ShowMsgWBeep, 0);
        }

        [TestMethod]
        public void LockCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/lock{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = 0,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(1, "lock", enuUnitCommand.Lock);
            check(1, "unlock", enuUnitCommand.Unlock);

            // Check all locks
            check(0, "lock", enuUnitCommand.Lock);

            // Check case insensitivity
            check(2, "LOCK", enuUnitCommand.Lock);
        }
    }
}


