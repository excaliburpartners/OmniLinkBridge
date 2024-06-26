﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Sensor : Device
    {
        public Sensor(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum DeviceClass
        {
            humidity,
            temperature
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceClass? device_class { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string unit_of_measurement { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string value_template { get; set; }
    }
}
