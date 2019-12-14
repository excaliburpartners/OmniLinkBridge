using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniLinkBridge.MQTT
{
    public class BinarySensor : Device
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum DeviceClass
        {
            battery,
            cold,
            door,
            garage_door,
            gas,
            heat,
            moisture,
            motion,
            problem,
            safety,
            smoke,
            window
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceClass? device_class { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string value_template { get; set; }
    }
}
