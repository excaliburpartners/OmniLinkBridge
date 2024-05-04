using Newtonsoft.Json;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Number : Device
    {
        public Number(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? min { get; set; } 

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? max { get; set; }
    }
}
