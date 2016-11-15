using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;

namespace HAILogger
{
    static class Event
    {
        public static void WriteVerbose(string value)
        {
            Trace.WriteLine(value);
            LogFile(TraceLevel.Verbose, "VERBOSE", value);
        }

        public static void WriteVerbose(string source, string value)
        {
            Trace.WriteLine("VERBOSE: " + source + ": " + value);
            LogFile(TraceLevel.Verbose, "VERBOSE: " + source, value);
        }

        public static void WriteInfo(string source, string value, bool alert)
        {
            WriteInfo(source, value);
            if (alert)
            {
                LogEvent(EventLogEntryType.Information, source, value);
                SendMail("Info", source, value);
            }
        }

        public static void WriteInfo(string source, string value)
        {
            Trace.WriteLine("INFO: " + source + ": " + value);
            LogFile(TraceLevel.Info, "INFO: " + source, value);
        }

        public static void WriteWarn(string source, string value, bool alert)
        {
            WriteWarn(source, value);
            if (alert)
                SendMail("Warn", source, value);
        }

        public static void WriteWarn(string source, string value)
        {
            Trace.WriteLine("WARN: " + source + ": " + value);
            LogFile(TraceLevel.Warning, "WARN: " + source, value);
            LogEvent(EventLogEntryType.Warning, source, value);
        }

        public static void WriteError(string source, string value)
        {
            Trace.WriteLine("ERROR: " + source + ": " + value);
            LogFile(TraceLevel.Error, "ERROR: " + source, value);
            LogEvent(EventLogEntryType.Error, source, value);
            SendMail("Error", source, value);
        }

        public static void WriteAlarm(string source, string value)
        {
            Trace.WriteLine("ALARM: " + source + ": " + value);
            LogFile(TraceLevel.Error, "ALARM: " + source, value);
            LogEvent(EventLogEntryType.Warning, source, value);

            if (Global.mail_alarm_to != null && Global.mail_alarm_to.Length > 0)
            {
                MailMessage mail = new MailMessage();
                mail.From = Global.mail_from;
                foreach (MailAddress address in Global.mail_alarm_to)
                    mail.To.Add(address);
                mail.Priority = MailPriority.High;
                mail.Subject = value;
                mail.Body = value;

                SmtpClient smtp = new SmtpClient(Global.mail_server, Global.mail_port);

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
                    string error = "An error occurred sending email notification\r\n" + ex.Message;
                    LogFile(TraceLevel.Error, "ERROR: " + source, error);
                    LogEvent(EventLogEntryType.Error, "EventNotification", error);
                }
            }
        }

        private static void SendMail(string level, string source, string value)
        {
            if (Global.mail_to == null || Global.mail_to.Length == 0)
                return;

            MailMessage mail = new MailMessage();
            mail.From = Global.mail_from;
            foreach (MailAddress address in Global.mail_to)
                mail.To.Add(address);
            mail.Subject = Global.event_source + " - " + level;
            mail.Body = source + ": " + value;

            SmtpClient smtp = new SmtpClient(Global.mail_server, Global.mail_port);

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
                string error = "An error occurred sending email notification\r\n" + ex.Message;
                LogFile(TraceLevel.Error, "ERROR: " + source, error);
                LogEvent(EventLogEntryType.Error, "EventNotification", error);
            }
        }

        private static void LogEvent(EventLogEntryType type, string source, string value)
        {
		    string event_log = "Application";

            if (!EventLog.SourceExists(Global.event_source))
                EventLog.CreateEventSource(Global.event_source, event_log);

            string event_msg = source + ": " + value;

            EventLog.WriteEntry(Global.event_source, event_msg, type, 234);
        }

        private static void LogFile(TraceLevel level, string source, string value)
        {
            TraceSwitch tswitch = new TraceSwitch("TraceLevelSwitch", "Trace Level for Entire Application");

            if (tswitch.Level < level)
                return;

            try
            {
                FileStream fs = new FileStream(Global.dir_config + "\\" + Global.event_log, FileMode.Append, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);

                sw.WriteLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss ") + source + ": " + value);

                sw.Close();
                fs.Close();
            }
            catch
            {
                LogEvent(EventLogEntryType.Error, "EventLogger", "Unable to write to the file log.");
            }
        }
    }
}
