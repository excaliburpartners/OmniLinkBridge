namespace OmniLinkBridge.Notifications
{
    public enum NotificationPriority
    {
        /// <summary>
        /// Generate no notification/alert
        /// </summary>
        VeryLow = -2,

        /// <summary>
        /// Always send as a quiet notification
        /// </summary>
        Moderate = -1,

        /// <summary>
        /// Trigger sound, vibration, and display an alert according to the user's device settings
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Display as high-priority and bypass the user's quiet hours
        /// </summary>
        High = 1,

        /// <summary>
        /// Require confirmation from the user
        /// </summary>
        Emergency = 2,
    };
}
