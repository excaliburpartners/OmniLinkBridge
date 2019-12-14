using Newtonsoft.Json;
using OmniLinkBridge.Modules;

namespace OmniLinkBridge.MQTT
{
    public class Device
    {
        public string unique_id { get; set; }

        public string name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state_topic { get; set; }

        public string availability_topic { get; set; } = $"{Global.mqtt_prefix}/status";

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceRegistry device { get; set; } = MQTTModule.MqttDeviceRegistry;
    }
}
