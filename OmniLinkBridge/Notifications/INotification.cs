namespace OmniLinkBridge.Notifications
{
    public interface INotification
    {
        void Notify(string source, string description, NotificationPriority priority);
    }
}
