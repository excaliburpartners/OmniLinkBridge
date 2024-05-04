using Newtonsoft.Json;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Switch : Device
    {
        public Switch(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_off { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_on { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string value_template { get; set; }
    }
}
