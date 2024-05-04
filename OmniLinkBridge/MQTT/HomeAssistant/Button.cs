using Newtonsoft.Json;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Button : Device
    {
        public Button(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string payload_press { get; set; }
    }
}
