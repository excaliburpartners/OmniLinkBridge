namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Light : Device
    {
        public Light(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        public string command_topic { get; set; }

        public string brightness_state_topic { get; set; }

        public string brightness_command_topic { get; set; }

        public int brightness_scale { get; private set; } = 100;
    }
}
