namespace OmniLinkBridge.MQTT
{
    public class Availability
    {
        public string topic { get; set; } = $"{Global.mqtt_prefix}/status";
    }
}
