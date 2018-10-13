using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge.Notifications;

namespace OmniLinkBridgeTest
{
    [TestClass]
    public class NotificationTest
    {
        [TestMethod]
        public void SendNotification()
        {
            Notification.Notify("Title", "Description");
        }
    }
}
