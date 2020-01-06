using OmniLinkBridge.Notifications;
using OmniLinkBridge.OmniLink;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Reflection;
using System.Threading;

namespace OmniLinkBridge.Modules
{
    public class LoggerModule : IModule
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private bool running = true;

        private readonly OmniLinkII omnilink;
        private readonly List<string> alarms = new List<string>();

        // mySQL Database
        private OdbcConnection mysql_conn = null;
        private DateTime mysql_retry = DateTime.MinValue;
        private OdbcCommand mysql_command = null;
        private readonly Queue<string> mysql_queue = new Queue<string>();
        private readonly object mysql_lock = new object();

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        public LoggerModule(OmniLinkII omni)
        {
            omnilink = omni;
            omnilink.OnAreaStatus += Omnilink_OnAreaStatus;
            omnilink.OnZoneStatus += Omnilink_OnZoneStatus;
            omnilink.OnThermostatStatus += Omnilink_OnThermostatStatus;
            omnilink.OnUnitStatus += Omnilink_OnUnitStatus;
            omnilink.OnMessageStatus += Omnilink_OnMessageStatus;
            omnilink.OnSystemStatus += Omnilink_OnSystemStatus;
        }

        public void Startup()
        {
            if (Global.mysql_logging)
            {
                log.Information("Connecting to database");

                mysql_conn = new OdbcConnection(Global.mysql_connection);
            }

            while (true)
            {
                // End gracefully when not logging or database queue empty
                if (!running && (!Global.mysql_logging || DBQueueCount() == 0))
                    break;

                // Make sure database connection is active
                if (Global.mysql_logging && mysql_conn.State != ConnectionState.Open)
                {
                    // Nothing we can do if shutting down
                    if (!running)
                        break;

                    if (mysql_retry < DateTime.Now)
                        DBOpen();

                    if (mysql_conn.State != ConnectionState.Open)
                    {
                        // Loop to prevent database queries from executing
                        trigger.WaitOne(new TimeSpan(0, 0, 1));
                        continue;
                    }
                }

                // Sleep when not logging or database queue empty
                if (!Global.mysql_logging || DBQueueCount() == 0)
                {
                    trigger.WaitOne(new TimeSpan(0, 0, 1));
                    continue;
                }

                // Grab a copy in case the database query fails
                string query;
                lock (mysql_lock)
                    query = mysql_queue.Peek();

                try
                {
                    // Execute the database query
                    mysql_command = new OdbcCommand(query, mysql_conn);
                    mysql_command.ExecuteNonQuery();

                    // Successful remove query from queue
                    lock (mysql_lock)
                        mysql_queue.Dequeue();
                }
                catch (Exception ex)
                {
                    if (mysql_conn.State != ConnectionState.Open)
                    {
                        log.Warning("Lost connection to database");
                    }
                    else
                    {
                        log.Error(ex, "Error executing {query}", query);

                        // Prevent an endless loop from failed query
                        lock (mysql_lock)
                            mysql_queue.Dequeue();
                    }
                }
            }

            if (Global.mysql_logging)
                DBClose();
        }

        public void Shutdown()
        {
            running = false;
            trigger.Set();
        }

        private void Omnilink_OnAreaStatus(object sender, AreaStatusEventArgs e)
        {
            // Alarm notifcation
            if (e.Area.AreaFireAlarmText != "OK")
            {
                Notification.Notify("ALARM", "FIRE " + e.Area.Name + " " + e.Area.AreaFireAlarmText, NotificationPriority.Emergency);

                if (!alarms.Contains("FIRE" + e.ID))
                    alarms.Add("FIRE" + e.ID);
            }
            else if (alarms.Contains("FIRE" + e.ID))
            {
                Notification.Notify("ALARM CLEARED", "FIRE " + e.Area.Name + " " + e.Area.AreaFireAlarmText, NotificationPriority.High);

                alarms.Remove("FIRE" + e.ID);
            }

            if (e.Area.AreaBurglaryAlarmText != "OK")
            {
                Notification.Notify("ALARM", "BURGLARY " + e.Area.Name + " " + e.Area.AreaBurglaryAlarmText, NotificationPriority.Emergency);

                if (!alarms.Contains("BURGLARY" + e.ID))
                    alarms.Add("BURGLARY" + e.ID);
            }
            else if (alarms.Contains("BURGLARY" + e.ID))
            {
                Notification.Notify("ALARM CLEARED", "BURGLARY " + e.Area.Name + " " + e.Area.AreaBurglaryAlarmText, NotificationPriority.High);

                alarms.Remove("BURGLARY" + e.ID);
            }

            if (e.Area.AreaAuxAlarmText != "OK")
            {
                Notification.Notify("ALARM", "AUX " + e.Area.Name + " " + e.Area.AreaAuxAlarmText, NotificationPriority.Emergency);

                if (!alarms.Contains("AUX" + e.ID))
                    alarms.Add("AUX" + e.ID);
            }
            else if (alarms.Contains("AUX" + e.ID))
            {
                Notification.Notify("ALARM CLEARED", "AUX " + e.Area.Name + " " + e.Area.AreaAuxAlarmText, NotificationPriority.High);

                alarms.Remove("AUX" + e.ID);
            }

            if (e.Area.AreaDuressAlarmText != "OK")
            {
                Notification.Notify("ALARM", "DURESS " + e.Area.Name + " " + e.Area.AreaDuressAlarmText, NotificationPriority.Emergency);

                if (!alarms.Contains("DURESS" + e.ID))
                    alarms.Add("DURESS" + e.ID);
            }
            else if (alarms.Contains("DURESS" + e.ID))
            {
                Notification.Notify("ALARM CLEARED", "DURESS " + e.Area.Name + " " + e.Area.AreaDuressAlarmText, NotificationPriority.High);

                alarms.Remove("DURESS" + e.ID);
            }

            string status = e.Area.ModeText();

            if (e.Area.ExitTimer > 0)
                status = "ARMING " + status;

            if (e.Area.EntryTimer > 0)
                status = "TRIPPED " + status;

            DBQueue(@"
            INSERT INTO log_areas (timestamp, id, name, 
                fire, police, auxiliary, 
                duress, security)
            VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + e.ID.ToString() + "','" + e.Area.Name + "','" +
                    e.Area.AreaFireAlarmText + "','" + e.Area.AreaBurglaryAlarmText + "','" + e.Area.AreaAuxAlarmText + "','" +
                    e.Area.AreaDuressAlarmText + "','" + status + "')");

            if (Global.verbose_area)
                log.Verbose("AreaStatus {id} {name}, Status: {status}", e.ID, e.Area.Name, status);

            if (Global.notify_area && e.Area.LastMode != e.Area.AreaMode)
                Notification.Notify("Security", e.Area.Name + " " + e.Area.ModeText());
        }

        private void Omnilink_OnZoneStatus(object sender, ZoneStatusEventArgs e)
        {
            DBQueue(@"
                INSERT INTO log_zones (timestamp, id, name, status)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + e.ID + "','" + e.Zone.Name + "','" + e.Zone.StatusText() + "')");

            if (Global.verbose_zone)
            {
                if (e.Zone.IsTemperatureZone())
                    log.Verbose("ZoneStatus {id} {name}, Temp: {temp}", e.ID, e.Zone.Name, e.Zone.TempText());
                else
                    log.Verbose("ZoneStatus {id} {name}, Status: {status}", e.ID, e.Zone.Name, e.Zone.StatusText());
            }
        }

        private void Omnilink_OnThermostatStatus(object sender, ThermostatStatusEventArgs e)
        {
            int.TryParse(e.Thermostat.TempText(), out int temp);
            int.TryParse(e.Thermostat.HeatSetpointText(), out int heat);
            int.TryParse(e.Thermostat.CoolSetpointText(), out int cool);
            int.TryParse(e.Thermostat.HumidityText(), out int humidity);
            int.TryParse(e.Thermostat.HumidifySetpointText(), out int humidify);
            int.TryParse(e.Thermostat.DehumidifySetpointText(), out int dehumidify);

            // Log all events including thermostat polling
            DBQueue(@"
                INSERT INTO log_thermostats (timestamp, id, name, 
                    status, temp, heat, cool, 
                    humidity, humidify, dehumidify,
                    mode, fan, hold)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "','" + e.ID + "','" + e.Thermostat.Name + "','" +
                    e.Thermostat.HorC_StatusText() + "','" + temp.ToString() + "','" + heat + "','" + cool + "','" +
                    humidity + "','" + humidify + "','" + dehumidify + "','" +
                    e.Thermostat.ModeText() + "','" + e.Thermostat.FanModeText() + "','" + e.Thermostat.HoldStatusText() + "')");

            // Ignore events fired by thermostat polling
            if (!e.EventTimer && Global.verbose_thermostat)
                log.Verbose("ThermostatStatus {id} {name}, Status: {temp} {status}, " +
                    "Heat {heat}, Cool: {cool}, Mode: {mode}, Fan: {fan}, Hold: {hold}",
                    e.ID, e.Thermostat.Name, 
                    e.Thermostat.TempText(), e.Thermostat.HorC_StatusText(),
                    e.Thermostat.HeatSetpointText(), 
                    e.Thermostat.CoolSetpointText(),
                    e.Thermostat.ModeText(),
                    e.Thermostat.FanModeText(),
                    e.Thermostat.HoldStatusText());
        }

        private void Omnilink_OnUnitStatus(object sender, UnitStatusEventArgs e)
        {
            string status = e.Unit.StatusText;

            if (e.Unit.Status == 100 && e.Unit.StatusTime == 0)
                status = "OFF";
            else if (e.Unit.Status == 200 && e.Unit.StatusTime == 0)
                status = "ON";

            DBQueue(@"
                INSERT INTO log_units (timestamp, id, name, 
                    status, statusvalue, statustime)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + e.ID + "','" + e.Unit.Name + "','" +
                    status + "','" + e.Unit.Status + "','" + e.Unit.StatusTime + "')");

            if (Global.verbose_unit)
                log.Verbose("UnitStatus {id} {name}, Status: {status}", e.ID, e.Unit.Name, status);
        }

        private void Omnilink_OnMessageStatus(object sender, MessageStatusEventArgs e)
        {
            DBQueue(@"
                INSERT INTO log_messages (timestamp, id, name, status)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + e.ID + "','" + e.Message.Name + "','" + e.Message.StatusText() + "')");

            if (Global.verbose_message)
                log.Verbose("MessageStatus {id} {name}, Status: {status}", e.ID, e.Message.Name, e.Message.StatusText());

            if (Global.notify_message)
                Notification.Notify("Message", e.ID + " " + e.Message.Name + ", " + e.Message.StatusText());
        }

        private void Omnilink_OnSystemStatus(object sender, SystemStatusEventArgs e)
        {
            DBQueue(@"
                INSERT INTO log_events (timestamp, name, status)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + e.Type.ToString() + "','" + e.Value + "')");

            if (Global.verbose_event)
                log.Verbose("SystemEvent {name} {status}", e.Type.ToString(), e.Value);

            if (e.SendNotification)
                Notification.Notify("SystemEvent", e.Type.ToString() + " " + e.Value);
        }

        public bool DBOpen()
        {
            try
            {
                if (mysql_conn.State != ConnectionState.Open)
                    mysql_conn.Open();

                mysql_retry = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to connect to database");
                mysql_retry = DateTime.Now.AddMinutes(1);
                return false;
            }

            return true;
        }

        public void DBClose()
        {
            if (mysql_conn.State != ConnectionState.Closed)
                mysql_conn.Close();
        }

        public void DBQueue(string query)
        {
            if (!Global.mysql_logging)
                return;

            lock (mysql_lock)
                mysql_queue.Enqueue(query);
        }

        private int DBQueueCount()
        {
            int count;
            lock (mysql_lock)
                count = mysql_queue.Count;

            return count;
        }
    }
}
