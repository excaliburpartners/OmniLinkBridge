using log4net;
using System;
using System.Net;
using System.Net.Mail;
using System.Reflection;

namespace OmniLinkBridge.Notifications
{
    public class EmailNotification : INotification
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void Notify(string source, string description, NotificationPriority priority)
        {
            if (string.IsNullOrEmpty(Global.mail_server))
                return;

            foreach (MailAddress address in Global.mail_to)
            {
                MailMessage mail = new MailMessage
                {
                    From = Global.mail_from,
                    Subject = $"{Global.controller_name} - {source}",
                    Body = $"{source}: {description}"
                };
                mail.To.Add(address);

                using (SmtpClient smtp = new SmtpClient(Global.mail_server, Global.mail_port))
                {
                    smtp.EnableSsl = Global.mail_tls;
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
                        log.Error("An error occurred sending email notification", ex);
                    }
                }
            }
        }
    }
}
