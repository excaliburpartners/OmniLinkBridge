using Newtonsoft.Json;

namespace OmniLinkBridge.MQTT
{
    public class Number : Device
    {
        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string icon { get; set; }
    }
}
