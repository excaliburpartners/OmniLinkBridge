using log4net;
using System;
using System.Net;
using System.Net.Mail;
using System.Reflection;

namespace OmniLinkBridge.Notifications
{
    public class EmailNotification : INotification
    {
        private static ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void Notify(string source, string description, NotificationPriority priority)
        {
            foreach (MailAddress address in Global.mail_to)
            {
                MailMessage mail = new MailMessage();
                mail.From = Global.mail_from;
                mail.To.Add(address);
                mail.Subject = "OmniLinkBridge - " + source;
                mail.Body = source + ": " + description;

                using (SmtpClient smtp = new SmtpClient(Global.mail_server, Global.mail_port))
                {
                    if (!string.IsNullOrEmpty(Global.mail_username))
                    {
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(Global.mail_username, Global.mail_password);
                    }

                    try
                    {
                        smtp.Send(mail);
                    }
                    catch (Exception ex)
                    {
                        log.Error("An error occurred sending notification", ex);
                    }
                }
            }
        }
    }
}
