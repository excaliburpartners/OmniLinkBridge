namespace OmniLinkBridge.MQTT
{
    public class AreaCommandCode
    {
        public bool Success { get; set; } = true;
        public string Command { get; set; }
        public bool Validate { get; set; }
        public int Code { get; set; }
    }
}
