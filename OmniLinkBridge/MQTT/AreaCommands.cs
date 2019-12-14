namespace OmniLinkBridge.MQTT
{
    enum AreaCommands
    {
        disarm,
        arm_home,
        arm_away,
        arm_night,
        // The below aren't supported by Home Assistant
        arm_home_instant,
        arm_night_delay,
        arm_vacation
    }
}
