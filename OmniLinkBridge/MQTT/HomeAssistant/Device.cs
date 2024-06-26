﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Device
    {
        public Device(DeviceRegistry deviceRegistry)
        {
            device = deviceRegistry;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum AvailabilityMode
        {
            all,
            any,
            latest
        }

        public string unique_id { get; set; }

        public string name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string icon { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string availability_topic { get; set; } = $"{Global.mqtt_prefix}/status";

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Availability> availability { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public AvailabilityMode? availability_mode { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceRegistry device { get; set; }
    }
}
