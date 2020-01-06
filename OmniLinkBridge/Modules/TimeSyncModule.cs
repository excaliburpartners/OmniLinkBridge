using HAI_Shared;
using Serilog;
using System;
using System.Reflection;
using System.Threading;

namespace OmniLinkBridge.Modules
{
    public class TimeSyncModule : IModule
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private OmniLinkII OmniLink { get; set; }

        private readonly System.Timers.Timer tsync_timer = new System.Timers.Timer();
        private DateTime tsync_check = DateTime.MinValue;

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        public TimeSyncModule(OmniLinkII omni)
        {
            OmniLink = omni;
        }

        public void Startup()
        {
            tsync_timer.Elapsed += tsync_timer_Elapsed;
            tsync_timer.AutoReset = false;

            tsync_check = DateTime.MinValue;

            tsync_timer.Interval = TimeTimerInterval();
            tsync_timer.Start();

            // Wait until shutdown
            trigger.WaitOne();

            tsync_timer.Stop();
        }

        public void Shutdown()
        {
            trigger.Set();
        }

        static double TimeTimerInterval()
        {
            DateTime now = DateTime.Now;
            return ((60 - now.Second) * 1000 - now.Millisecond);
        }

        private void tsync_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (tsync_check.AddMinutes(Global.time_interval) < DateTime.Now)
                OmniLink.Controller.Connection.Send(new clsOL2MsgRequestSystemStatus(OmniLink.Controller.Connection), HandleRequestSystemStatus);

            tsync_timer.Interval = TimeTimerInterval();
            tsync_timer.Start();
        }

        private void HandleRequestSystemStatus(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            tsync_check = DateTime.Now;

            clsOL2MsgSystemStatus MSG = new clsOL2MsgSystemStatus(OmniLink.Controller.Connection, B);

            DateTime time;
            try
            {
                // The controller uses 2 digit years and C# uses 4 digit years
                // Extract the 2 digit prefix to use when parsing the time
                int year = DateTime.Now.Year / 100;

                time = new DateTime((int)MSG.Year + (year * 100), (int)MSG.Month, (int)MSG.Day, (int)MSG.Hour, (int)MSG.Minute, (int)MSG.Second);
            }
            catch
            {
                log.Warning("Controller time could not be parsed");

                DateTime now = DateTime.Now;
                OmniLink.Controller.Connection.Send(new clsOL2MsgSetTime(OmniLink.Controller.Connection, (byte)(now.Year % 100), (byte)now.Month, (byte)now.Day, (byte)now.DayOfWeek,
                    (byte)now.Hour, (byte)now.Minute, (byte)(now.IsDaylightSavingTime() ? 1 : 0)), HandleSetTime);

                return;
            }

            double adj = (DateTime.Now - time).Duration().TotalSeconds;

            if (adj > Global.time_drift)
            {
                log.Warning("Controller time {controllerTime} out of sync by {driftSeconds} seconds",
                    time.ToString("MM/dd/yyyy HH:mm:ss"),  adj);

                DateTime now = DateTime.Now;
                OmniLink.Controller.Connection.Send(new clsOL2MsgSetTime(OmniLink.Controller.Connection, (byte)(now.Year % 100), (byte)now.Month, (byte)now.Day, (byte)now.DayOfWeek,
                    (byte)now.Hour, (byte)now.Minute, (byte)(now.IsDaylightSavingTime() ? 1 : 0)), HandleSetTime);
            }
        }

        private void HandleSetTime(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            log.Debug("Controller time has been successfully set");
        }
    }
}
