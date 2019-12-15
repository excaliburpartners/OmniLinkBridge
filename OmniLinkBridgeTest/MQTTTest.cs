using System;
using System.Text;
using System.Collections.Generic;
using HAI_Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge.MQTT;
using OmniLinkBridgeTest.Mock;

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
            void check(ushort id, string payload, enuUnitCommand command)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/area{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = 0,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            // First area standard format
            check(1, "disarm", enuUnitCommand.SecurityOff);
            check(1, "arm_home", enuUnitCommand.SecurityDay);
            check(1, "arm_away", enuUnitCommand.SecurityAway);
            check(1, "arm_night", enuUnitCommand.SecurityNight);
            check(1, "arm_home_instant", enuUnitCommand.SecurityDyi);
            check(1, "arm_night_delay", enuUnitCommand.SecurityNtd);
            check(1, "arm_vacation", enuUnitCommand.SecurityVac);

            // Last area with case check
            check(8, "DISARM", enuUnitCommand.SecurityOff);
        }

        [TestMethod]
        public void ZoneCommand()
        {
            void check(ushort id, string payload, enuUnitCommand command)
            {
                SendCommandEventArgs actual = null;
                omniLink.OnSendCommand += (sender, e) => { actual = e; };
                messageProcessor.Process($"omnilink/zone{id}/command", payload);
                SendCommandEventArgs expected = new SendCommandEventArgs()
                {
                    Cmd = command,
                    Par = 0,
                    Pr2 = id
                };
                Assert.AreEqual(expected, actual);
            }

            check(1, "bypass", enuUnitCommand.Bypass);
            check(1, "restore", enuUnitCommand.Restore);

            check(2, "BYPASS", enuUnitCommand.Bypass);
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
    }
}


