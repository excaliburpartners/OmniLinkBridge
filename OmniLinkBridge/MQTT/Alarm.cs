using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniLinkBridge.MQTT
{
    public class Alarm : Device
    {
        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string command_template { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string code { get; set; }
    }
}
