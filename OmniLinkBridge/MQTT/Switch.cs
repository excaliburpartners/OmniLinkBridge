using Newtonsoft.Json;

namespace OmniLinkBridge.MQTT
{
    public class Switch : Device
    {
        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_off { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_on { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string value_template { get; set; }
    }
}
