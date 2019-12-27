using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge.Notifications;
using OmniLinkBridge;
using System.Net.Mail;

namespace OmniLinkBridgeTest
{
    [TestClass]
    public class NotificationTest
    {
        [TestMethod]
        public void SendNotification()
        {
            // This is an integration test
            Global.mail_server = "localhost";
            Global.mail_tls = false;
            Global.mail_port = 25;
            Global.mail_from = new MailAddress("OmniLinkBridge@localhost");
            Global.mail_to = new MailAddress[]
            {
                new MailAddress("mailbox@localhost")
            };

            Notification.Notify("Title", "Description");
        }
    }
}
