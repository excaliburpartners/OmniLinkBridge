using Newtonsoft.Json;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Lock : Device
    {
        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_lock { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_unlock { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state_locked { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state_unlocked { get; set; }
    }
}
