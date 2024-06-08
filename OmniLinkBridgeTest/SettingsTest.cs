﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge;
using OmniLinkBridge.MQTT.HomeAssistant;
using System;
using System.Collections.Generic;
using ha = OmniLinkBridge.MQTT.HomeAssistant;

namespace OmniLinkBridgeTest
{
    [TestClass]
    public class SettingsTest
    {
        private string[] RequiredSettings()
        {
            return new string[]
            {
                "controller_address = 1.1.1.1",
                "controller_port = 4369",
                "controller_key1 = 00-00-00-00-00-00-00-01",
                "controller_key2 = 00-00-00-00-00-00-00-02",
            };
        }

        [TestMethod]
        public void TestControllerSettings()
        {
            Assert.ThrowsException<Exception>(() => Settings.LoadSettings(new string[]
            {
                "controller_address="
            }));

            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "controller_name = MyController"
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual("1.1.1.1", Global.controller_address);
            Assert.AreEqual(4369, Global.controller_port);
            Assert.AreEqual("0000000000000001", Global.controller_key1);
            Assert.AreEqual("0000000000000002", Global.controller_key2);
            Assert.AreEqual("MyController", Global.controller_name);
        }

        [TestMethod]
        public void TestTimeSyncSettings()
        {
            // Default should be false
            Settings.LoadSettings(RequiredSettings());
            Assert.AreEqual(false, Global.mqtt_enabled);

            // Test minimal settings
            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "time_sync = yes",
                "time_interval = 60",
                "time_drift = 10"
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual(true, Global.time_sync);
            Assert.AreEqual(60, Global.time_interval);
            Assert.AreEqual(10, Global.time_drift);
        }

        [TestMethod]
        public void TestVerboseSettings()
        {
            // Default should be false
            Settings.LoadSettings(RequiredSettings());
            Assert.AreEqual(false, Global.verbose_unhandled);

            // Check each setting correctly sets
            foreach (string setting in new string[] {
                "verbose_unhandled",
                "verbose_event",
                "verbose_area",
                "verbose_zone",
                "verbose_thermostat_timer",
                "verbose_thermostat",
                "verbose_unit",
                "verbose_message",
                "verbose_lock"
            })
            {
                List<string> lines = new List<string>(RequiredSettings())
                {
                    $"{setting} = yes"
                };
                Settings.LoadSettings(lines.ToArray());
                Assert.AreEqual(true, Global.GetValue(setting));
            }
        }

        [TestMethod]
        public void TestWebAPISettings()
        {
            // Default should be false
            Settings.LoadSettings(RequiredSettings());
            Assert.AreEqual(false, Global.mqtt_enabled);

            // Test minimal settings
            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "webapi_enabled = yes",
                "webapi_port = 8000",
                "webapi_override_zone = id=20;device_type=motion"
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual(true, Global.webapi_enabled);
            Assert.AreEqual(8000, Global.webapi_port);

            Dictionary<int, OmniLinkBridge.WebAPI.OverrideZone> override_zone = new Dictionary<int, OmniLinkBridge.WebAPI.OverrideZone>()
            {
                { 20, new OmniLinkBridge.WebAPI.OverrideZone { device_type = OmniLinkBridge.WebAPI.DeviceType.motion }}
            };

            Assert.AreEqual(override_zone.Count, Global.webapi_override_zone.Count);
            foreach (KeyValuePair<int, OmniLinkBridge.WebAPI.OverrideZone> pair in override_zone)
            {
                Global.webapi_override_zone.TryGetValue(pair.Key, out OmniLinkBridge.WebAPI.OverrideZone value);
                Assert.AreEqual(override_zone[pair.Key].device_type, value.device_type);
            }
        }

        [TestMethod]
        public void TestMQTTSettings()
        {
            // Default should be false
            Settings.LoadSettings(RequiredSettings());
            Assert.AreEqual(false, Global.mqtt_enabled);

            // Test minimal settings
            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "mqtt_enabled = yes",
                "mqtt_server = localhost",
                "mqtt_port = 1883"
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual(true, Global.mqtt_enabled);
            Assert.AreEqual("localhost", Global.mqtt_server);
            Assert.AreEqual(1883, Global.mqtt_port);
            Assert.AreEqual("omnilink", Global.mqtt_prefix);
            Assert.AreEqual("homeassistant", Global.mqtt_discovery_prefix);
            Assert.AreEqual(string.Empty, Global.mqtt_discovery_name_prefix);

            // Test additional settings
            lines.AddRange(new string[]
            {
                "mqtt_username = myuser",
                "mqtt_password = mypass",
                "mqtt_prefix = myprefix",
                "mqtt_discovery_prefix = mydiscoveryprefix",
                "mqtt_discovery_name_prefix = mynameprefix",
                "mqtt_discovery_ignore_zones = 1,2-3,4",
                "mqtt_discovery_ignore_units = 2-5,7",
                "mqtt_discovery_override_area = id=1",
                "mqtt_discovery_override_area = id=2;code_arm=true;code_disarm=true;arm_home=false;arm_away=false;arm_night=false;arm_vacation=false",
                "mqtt_discovery_override_zone = id=5;device_class=garage_door",
                "mqtt_discovery_override_zone = id=7;device_class=motion",
                "mqtt_discovery_override_unit = id=1;type=switch",
                "mqtt_discovery_override_unit = id=395;type=light",
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual("myuser", Global.mqtt_username);
            Assert.AreEqual("mypass", Global.mqtt_password);
            Assert.AreEqual("myprefix", Global.mqtt_prefix);
            Assert.AreEqual("mydiscoveryprefix", Global.mqtt_discovery_prefix);
            Assert.AreEqual("mynameprefix ", Global.mqtt_discovery_name_prefix);
            Assert.IsTrue(Global.mqtt_discovery_ignore_zones.SetEquals(new int[] { 1, 2, 3, 4 }));
            Assert.IsTrue(Global.mqtt_discovery_ignore_units.SetEquals(new int[] { 2, 3, 4, 5, 7 }));

            Dictionary<int, OmniLinkBridge.MQTT.OverrideArea> override_area = new Dictionary<int, OmniLinkBridge.MQTT.OverrideArea>()
            {
                { 1, new OmniLinkBridge.MQTT.OverrideArea { }},
                { 2, new OmniLinkBridge.MQTT.OverrideArea { code_arm = true, code_disarm = true, 
                    arm_home = false, arm_away = false, arm_night = false, arm_vacation = false }},
            };

            Assert.AreEqual(override_area.Count, Global.mqtt_discovery_override_area.Count);
            foreach (KeyValuePair<int, OmniLinkBridge.MQTT.OverrideArea> pair in override_area)
            {
                Global.mqtt_discovery_override_area.TryGetValue(pair.Key, out OmniLinkBridge.MQTT.OverrideArea value);
                Assert.AreEqual(override_area[pair.Key].code_arm, value.code_arm);
                Assert.AreEqual(override_area[pair.Key].code_disarm, value.code_disarm);
                Assert.AreEqual(override_area[pair.Key].arm_home, value.arm_home);
                Assert.AreEqual(override_area[pair.Key].arm_away, value.arm_away);
                Assert.AreEqual(override_area[pair.Key].arm_night, value.arm_night);
                Assert.AreEqual(override_area[pair.Key].arm_vacation, value.arm_vacation);
            }

            Dictionary<int, OmniLinkBridge.MQTT.OverrideZone> override_zone = new Dictionary<int, OmniLinkBridge.MQTT.OverrideZone>()
            {
                { 5, new OmniLinkBridge.MQTT.OverrideZone { device_class = ha.BinarySensor.DeviceClass.garage_door }},
                { 7, new OmniLinkBridge.MQTT.OverrideZone { device_class = ha.BinarySensor.DeviceClass.motion }}
            };

            Assert.AreEqual(override_zone.Count, Global.mqtt_discovery_override_zone.Count);
            foreach (KeyValuePair<int, OmniLinkBridge.MQTT.OverrideZone> pair in override_zone)
            {
                Global.mqtt_discovery_override_zone.TryGetValue(pair.Key, out OmniLinkBridge.MQTT.OverrideZone value);
                Assert.AreEqual(override_zone[pair.Key].device_class, value.device_class);
            }

            Dictionary<int, OmniLinkBridge.MQTT.OverrideUnit> override_unit = new Dictionary<int, OmniLinkBridge.MQTT.OverrideUnit>()
            {
                { 1, new OmniLinkBridge.MQTT.OverrideUnit { type = OmniLinkBridge.MQTT.UnitType.@switch }},
                { 395, new OmniLinkBridge.MQTT.OverrideUnit { type = OmniLinkBridge.MQTT.UnitType.light }}
            };

            Assert.AreEqual(override_unit.Count, Global.mqtt_discovery_override_unit.Count);
            foreach (KeyValuePair<int, OmniLinkBridge.MQTT.OverrideUnit> pair in override_unit)
            {
                Global.mqtt_discovery_override_unit.TryGetValue(pair.Key, out OmniLinkBridge.MQTT.OverrideUnit value);
                Assert.AreEqual(override_unit[pair.Key].type, value.type);
            }

            Assert.AreEqual(Global.mqtt_discovery_button_type, typeof(Switch));

            // Test additional settings
            lines.AddRange(new string[]
            {
                "mqtt_discovery_button_type = button"
            });
            Settings.LoadSettings(lines.ToArray());

            Assert.AreEqual(Global.mqtt_discovery_button_type, typeof(Button));
        }

        [TestMethod]
        public void TestNotifySettings()
        {
            // Default should be false
            Settings.LoadSettings(RequiredSettings());
            Assert.AreEqual(false, Global.verbose_unhandled);

            // Check each setting correctly sets
            foreach (string setting in new string[] {
                "notify_area",
                "notify_message"
            })
            {
                List<string> lines = new List<string>(RequiredSettings())
                {
                    $"{setting} = yes"
                };
                Settings.LoadSettings(lines.ToArray());
                Assert.AreEqual(true, Global.GetValue(setting));
            }
        }

        [TestMethod]
        public void TestMailSettings()
        {
            // Default should be null
            Settings.LoadSettings(RequiredSettings());
            Assert.AreEqual(null, Global.mail_server);

            // Test minimal settings
            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "mail_server = localhost",
                "mail_port = 25",
                "mail_from = from@localhost",
                "mail_to = to1@localhost",
                "mail_to = to2@localhost"
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual("localhost", Global.mail_server);
            Assert.AreEqual(false, Global.mail_tls);
            Assert.AreEqual(25, Global.mail_port);
            Assert.AreEqual("from@localhost", Global.mail_from.Address);
            Assert.AreEqual(2, Global.mail_to.Length);
            Assert.AreEqual("to1@localhost", Global.mail_to[0].Address);
            Assert.AreEqual("to2@localhost", Global.mail_to[1].Address);

            // Test additional settings
            lines.AddRange(new string[]
            {
                "mail_tls = yes",
                "mail_username = myuser",
                "mail_password = mypass"
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual(true, Global.mail_tls);
            Assert.AreEqual("myuser", Global.mail_username);
            Assert.AreEqual("mypass", Global.mail_password);
        }

        [TestMethod]
        public void TestProwlSettings()
        {
            // Test minimal settings
            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "prowl_key = mykey1",
                "prowl_key = mykey2",
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual("mykey1", Global.prowl_key[0]);
            Assert.AreEqual("mykey2", Global.prowl_key[1]);
        }

        [TestMethod]
        public void TestPushoverSettings()
        {
            // Test minimal settings
            List<string> lines = new List<string>(RequiredSettings());
            lines.AddRange(new string[]
            {
                "pushover_token = mytoken",
                "pushover_user = myuser1",
                "pushover_user = myuser2",
            });
            Settings.LoadSettings(lines.ToArray());
            Assert.AreEqual("mytoken", Global.pushover_token);
            Assert.AreEqual("myuser1", Global.pushover_user[0]);
            Assert.AreEqual("myuser2", Global.pushover_user[1]);
        }
    }
}
