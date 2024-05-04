using System.Collections.Generic;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Select : Device
    {
        public Select(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        public string command_topic { get; set; }

        public List<string> options { get; set; } = null;
    }
}
