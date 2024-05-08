using HAI_Shared;
using OmniLinkBridge.OmniLink;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniLinkBridge.Modules
{
    public class OmniLinkII : IModule, IOmniLinkII
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        private bool running = true;

        // OmniLink Controller
        public clsHAC Controller { get; private set; }
        private DateTime retry = DateTime.MinValue;

        public bool TroublePhone { get; set; }
        public bool TroubleAC { get; set; }
        public bool TroubleBattery { get; set; }
        public bool TroubleDCM { get; set; }

        // Thermostats
        private readonly Dictionary<ushort, DateTime> tstats = new Dictionary<ushort, DateTime>();
        private readonly System.Timers.Timer tstat_timer = new System.Timers.Timer();
        private readonly object tstat_lock = new object();

        // Events
        public event EventHandler<EventArgs> OnConnect;
        public event EventHandler<EventArgs> OnDisconnect;
        public event EventHandler<AreaStatusEventArgs> OnAreaStatus;
        public event EventHandler<ZoneStatusEventArgs> OnZoneStatus;
        public event EventHandler<ThermostatStatusEventArgs> OnThermostatStatus;
        public event EventHandler<UnitStatusEventArgs> OnUnitStatus;
        public event EventHandler<ButtonStatusEventArgs> OnButtonStatus;
        public event EventHandler<MessageStatusEventArgs> OnMessageStatus;
        public event EventHandler<LockStatusEventArgs> OnLockStatus;
        public event EventHandler<AudioZoneStatusEventArgs> OnAudioZoneStatus;
        public event EventHandler<SystemStatusEventArgs> OnSystemStatus;

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);
        private readonly AutoResetEvent nameWait = new AutoResetEvent(false);

        public OmniLinkII(string address, int port, string key1, string key2)
        {
            Controller = new clsHAC();
            
            Controller.Connection.NetworkAddress = address;
            Controller.Connection.NetworkPort = (ushort)port;
            Controller.Connection.ControllerKey = clsUtil.HexString2ByteArray(string.Concat(key1, key2));

            Controller.PreferredNetworkProtocol = clsHAC.enuPreferredNetworkProtocol.TCP;
            Controller.Connection.ConnectionType = enuOmniLinkConnectionType.Network_TCP;

            tstat_timer.Elapsed += tstat_timer_Elapsed;
            tstat_timer.AutoReset = false;
        }

        public void Startup()
        {
            while(running)
            { 
                // Make sure controller connection is active
                if (Controller.Connection.ConnectionState == enuOmniLinkConnectionState.Offline &&
                    retry < DateTime.Now)
                {
                    Connect();
                }

                trigger.WaitOne(new TimeSpan(0, 0, 5));
            }

            Disconnect();
        }

        public void Shutdown()
        {
            running = false;
            trigger.Set();
        }

        public bool SendCommand(enuUnitCommand Cmd, byte Par, ushort Pr2)
        {
            log.Verbose("Sending: {command}, Par1: {par1}, Par2: {par2}", Cmd, Par, Pr2);
            return Controller.SendCommand(Cmd, Par, Pr2);
        }

        #region Connection
        private void Connect()
        {
            if (Controller.Connection.ConnectionState == enuOmniLinkConnectionState.Offline)
            {
                retry = DateTime.Now.AddMinutes(1);

                log.Debug("Controller: {connectionStatus}", "Connect");
                Controller.Connection.Connect(HandleConnectStatus, HandleUnsolicitedPackets);
            }
        }

        private void Disconnect()
        {
            if (Controller.Connection.ConnectionState != enuOmniLinkConnectionState.Offline)
            {
                log.Debug("Controller: {connectionStatus}", "Disconnect");
                Controller.Connection.Disconnect();
            }
        }

        private void HandleConnectStatus(enuOmniLinkCommStatus CS)
        {
            var status = CS.ToString().ToSpaceTitleCase();

            switch (CS)
            {
                case enuOmniLinkCommStatus.Connecting:
                    log.Debug("Controller Status: {connectionStatus}", status);
                    break;
                case enuOmniLinkCommStatus.Connected:
                    IdentifyController();
                    break;
                case enuOmniLinkCommStatus.Disconnected:
                    log.Information("Controller Status: {connectionStatus}", status);
                    OnDisconnect?.Invoke(this, new EventArgs());
                    break;
                case enuOmniLinkCommStatus.InterruptedFunctionCall:
                    if (running)
                        log.Error("Controller Status: {connectionStatus}", status);
                    break;

                case enuOmniLinkCommStatus.Retrying:
                case enuOmniLinkCommStatus.OperationNowInProgress:
                case enuOmniLinkCommStatus.OperationAlreadyInProgress:
                case enuOmniLinkCommStatus.AlreadyConnected:
                    log.Warning("Controller Status: {connectionStatus}", status);
                    break;

                default:
                    log.Error("Controller Status: {connectionStatus}", status);
                    break;
            }
        }

        private void IdentifyController()
        {
            if (Controller.Connection.ConnectionState == enuOmniLinkConnectionState.Online ||
                Controller.Connection.ConnectionState == enuOmniLinkConnectionState.OnlineSecure)
            {
                Controller.Connection.Send(new clsOL2MsgRequestSystemInformation(Controller.Connection), HandleIdentifyController);
            }
        }

        private async void HandleIdentifyController(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            if ((B.Length > 3) && (B[2] == (byte)enuOmniLink2MessageType.SystemInformation))
            {
                clsOL2MsgSystemInformation MSG = new clsOL2MsgSystemInformation(Controller.Connection, B);

                foreach (enuModel enu in Enum.GetValues(typeof(enuModel)))
                {
                    if (enu == MSG.ModelNumber)
                    {
                        Controller.Model = enu;
                        break;
                    }
                }

                if (Controller.Model == MSG.ModelNumber)
                {
                    Controller.CopySystemInformation(MSG);

                    using (LogContext.PushProperty("Telemetry", "Controller"))
                        log.Information("Controller is {ControllerModel} firmware {ControllerVersion}",
                            Controller.GetModelText(), Controller.GetVersionText());

                    await ConnectedAsync();
                    return;
                }

                log.Error("Model does not match file");
                Controller.Connection.Disconnect();
            }
        }

        private async Task ConnectedAsync()
        {
            retry = DateTime.MinValue;

            await GetNamedProperties();
            UnsolicitedNotifications(true);

            tstat_timer.Interval = ThermostatTimerInterval();
            tstat_timer.Start();

            OnConnect?.Invoke(this, new EventArgs());

            Program.ShowSendLogsWarning();
        }
        #endregion

        #region Names
        private async Task GetNamedProperties()
        {
            log.Debug("Retrieving named units");

            await GetSystemFormats();
            await GetSystemTroubles();
            await GetNamed(enuObjectType.Area);
            await GetNamed(enuObjectType.Zone);
            await GetNamed(enuObjectType.Thermostat);
            await GetNamed(enuObjectType.Unit);
            await GetNamed(enuObjectType.Message);
            await GetNamed(enuObjectType.Button);
            await GetNamed(enuObjectType.AccessControlReader);
            await GetNamed(enuObjectType.AudioSource);
            await GetNamed(enuObjectType.AudioZone);
        }

        private async Task GetSystemFormats()
        {
            log.Debug("Waiting for system formats");

            var MSG = new clsOL2MsgRequestSystemFormats(Controller.Connection);
            Controller.Connection.Send(MSG, HandleNamedPropertiesResponse);

            await Task.Run(() =>
            {
                if(!nameWait.WaitOne(new TimeSpan(0, 0, 10)))
                    log.Error("Timeout occurred waiting system formats");
            });
        }

        private async Task GetSystemTroubles()
        {
            log.Debug("Waiting for system troubles");

            var MSG = new clsOL2MsgRequestSystemTroubles(Controller.Connection);
            Controller.Connection.Send(MSG, HandleNamedPropertiesResponse);

            await Task.Run(() =>
            {
                if(!nameWait.WaitOne(new TimeSpan(0, 0, 10)))
                    log.Error("Timeout occurred waiting for system troubles");
            });
        }

        private async Task GetNamed(enuObjectType type)
        {
            log.Debug("Waiting for named units {unitType}", type.ToString());

            GetNextNamed(type, 0);

            await Task.Run(() =>
            {
                if (!nameWait.WaitOne(new TimeSpan(0, 0, 30)))
                    log.Error("Timeout occurred waiting for named units {unitType}", type.ToString());
            });
        }

        private void GetNextNamed(enuObjectType type, int ix)
        {
            var MSG = new clsOL2MsgRequestProperties(Controller.Connection)
            {
                ObjectType = type,
                IndexNumber = (UInt16)ix,
                RelativeDirection = 1,  // next object after IndexNumber
                Filter1 = 1,  // (0=Named or Unnamed, 1=Named, 2=Unnamed).
                Filter2 = 0,  // Any Area
                Filter3 = 0  // Any Room
            };
            Controller.Connection.Send(MSG, HandleNamedPropertiesResponse);
        }

        private void HandleNamedPropertiesResponse(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            // does it look like a valid response
            if ((B.Length > 3) && (B[0] == 0x21))
            {
                switch ((enuOmniLink2MessageType)B[2])
                {
                    case enuOmniLink2MessageType.EOD:
                        nameWait.Set();
                        break;
                    case enuOmniLink2MessageType.SystemFormats:
                        var systemFormats = new clsOL2MsgSystemFormats(Controller.Connection, B);

                        Controller.DateFormat = systemFormats.Date;
                        Controller.TimeFormat = systemFormats.Time;
                        Controller.TempFormat = systemFormats.Temp;

                        using (LogContext.PushProperty("Telemetry", "TemperatureFormat"))
                            log.Debug("Temperature format is {TemperatureFormat}",
                                (Controller.TempFormat == enuTempFormat.Fahrenheit ? "Fahrenheit" : "Celsius"));

                        nameWait.Set();
                        break;
                    case enuOmniLink2MessageType.SystemTroubles:
                        var systemTroubles = new clsOL2MsgSystemTroubles(Controller.Connection, B);

                        TroublePhone = systemTroubles.Contains(enuTroubles.PhoneLine);
                        TroubleAC = systemTroubles.Contains(enuTroubles.AC);
                        TroubleBattery = systemTroubles.Contains(enuTroubles.BatteryLow);
                        TroubleDCM = systemTroubles.Contains(enuTroubles.DCM);

                        nameWait.Set();
                        break;
                    case enuOmniLink2MessageType.Properties:
                        clsOL2MsgProperties MSG = new clsOL2MsgProperties(Controller.Connection, B);

                        switch (MSG.ObjectType)
                        {
                            case enuObjectType.Area:
                                Controller.Areas.CopyProperties(MSG);
                                break;
                            case enuObjectType.Zone:
                                Controller.Zones.CopyProperties(MSG);

                                if (Controller.Zones[MSG.ObjectNumber].IsTemperatureZone() || Controller.Zones[MSG.ObjectNumber].IsHumidityZone())
                                    Controller.Connection.Send(new clsOL2MsgRequestExtendedStatus(Controller.Connection, 
                                        enuObjectType.Auxillary, MSG.ObjectNumber, MSG.ObjectNumber), HandleRequestAuxillaryStatus);

                                break;
                            case enuObjectType.Thermostat:
                                Controller.Thermostats.CopyProperties(MSG);

                                if (!tstats.ContainsKey(MSG.ObjectNumber))
                                    tstats.Add(MSG.ObjectNumber, DateTime.MinValue);
                                else
                                    tstats[MSG.ObjectNumber] = DateTime.MinValue;

                                Controller.Connection.Send(new clsOL2MsgRequestExtendedStatus(Controller.Connection, 
                                    enuObjectType.Thermostat, MSG.ObjectNumber, MSG.ObjectNumber), HandleRequestThermostatStatus);
                                log.Debug("Added thermostat to watch list {thermostatName}",
                                    Controller.Thermostats[MSG.ObjectNumber].Name);
                                break;
                            case enuObjectType.Unit:
                                Controller.Units.CopyProperties(MSG);
                                break;
                            case enuObjectType.Message:
                                Controller.Messages.CopyProperties(MSG);
                                break;
                            case enuObjectType.Button:
                                Controller.Buttons.CopyProperties(MSG);
                                break;
                            case enuObjectType.AccessControlReader:
                                Controller.AccessControlReaders.CopyProperties(MSG);
                                break;
                            case enuObjectType.AudioSource:
                                Controller.AudioSources.CopyProperties(MSG);
                                Controller.AudioSources[MSG.ObjectNumber].rawName = MSG.ObjectName;
                                break;
                            case enuObjectType.AudioZone:
                                Controller.AudioZones.CopyProperties(MSG);
                                Controller.AudioZones[MSG.ObjectNumber].rawName = MSG.ObjectName;
                                break;
                            default:
                                break;
                        }

                        GetNextNamed(MSG.ObjectType, MSG.ObjectNumber);
                        break;
                    default:
                        break;
                }
            }
        }

        #endregion

        #region Notifications
        private void UnsolicitedNotifications(bool enable)
        {
            log.Debug("Unsolicited notifications {status}", (enable ? "enabled" : "disabled"));
            Controller.Connection.Send(new clsOL2EnableNotifications(Controller.Connection, enable), null);
        }

        private bool HandleUnsolicitedPackets(byte[] B)
        {
            if ((B.Length > 3) && (B[0] == 0x21))
            {
                bool handled = false;

                switch ((enuOmniLink2MessageType)B[2])
                {
                    case enuOmniLink2MessageType.ClearNames:
                        break;
                    case enuOmniLink2MessageType.DownloadNames:
                        break;
                    case enuOmniLink2MessageType.UploadNames:
                        break;
                    case enuOmniLink2MessageType.NameData:
                        break;
                    case enuOmniLink2MessageType.ClearVoices:
                        break;
                    case enuOmniLink2MessageType.DownloadVoices:
                        break;
                    case enuOmniLink2MessageType.UploadVoices:
                        break;
                    case enuOmniLink2MessageType.VoiceData:
                        break;
                    case enuOmniLink2MessageType.Command:
                        break;
                    case enuOmniLink2MessageType.EnableNotifications:
                        break;
                    case enuOmniLink2MessageType.SystemInformation:
                        break;
                    case enuOmniLink2MessageType.SystemStatus:
                        break;
                    case enuOmniLink2MessageType.SystemTroubles:
                        break;
                    case enuOmniLink2MessageType.SystemFeatures:
                        break;
                    case enuOmniLink2MessageType.Capacities:
                        break;
                    case enuOmniLink2MessageType.Properties:
                        break;
                    case enuOmniLink2MessageType.Status:
                        break;
                    case enuOmniLink2MessageType.EventLogItem:
                        break;
                    case enuOmniLink2MessageType.ValidateCode:
                        break;
                    case enuOmniLink2MessageType.SystemFormats:
                        break;
                    case enuOmniLink2MessageType.Login:
                        break;
                    case enuOmniLink2MessageType.Logout:
                        break;
                    case enuOmniLink2MessageType.ActivateKeypadEmg:
                        break;
                    case enuOmniLink2MessageType.ExtSecurityStatus:
                        break;
                    case enuOmniLink2MessageType.CmdExtSecurity:
                        break;
                    case enuOmniLink2MessageType.AudioSourceStatus:
                        // Ignore audio source metadata status updates
                        handled = true;
                        break;
                    case enuOmniLink2MessageType.SystemEvents:
                        HandleUnsolicitedSystemEvent(B);
                        handled = true;
                        break;
                    case enuOmniLink2MessageType.ZoneReadyStatus:
                        break;
                    case enuOmniLink2MessageType.ExtendedStatus:
                        HandleUnsolicitedExtendedStatus(B);
                        handled = true;
                        break;
                    default:
                        break;
                }

                if (Global.verbose_unhandled && !handled)
                    log.Debug("Unhandled notification: " + ((enuOmniLink2MessageType)B[2]).ToString());
            }

            return true;
        }

        private void HandleUnsolicitedSystemEvent(byte[] B)
        {
            clsOL2SystemEvent MSG = new clsOL2SystemEvent(Controller.Connection, B);

            SystemStatusEventArgs eventargs = new SystemStatusEventArgs();

            if (MSG.SystemEvent >= 1 && MSG.SystemEvent <= 255)
            {
                eventargs.Type = SystemEventType.Button;
                eventargs.Value = ((int)MSG.SystemEvent).ToString() + " " + Controller.Buttons[MSG.SystemEvent].Name;

                OnSystemStatus?.Invoke(this, eventargs);

                OnButtonStatus?.Invoke(this, new ButtonStatusEventArgs()
                {
                    ID = MSG.SystemEvent,
                    Button = Controller.Buttons[MSG.SystemEvent]
                });
            }
            else if (MSG.SystemEvent >= (ushort)enuEventType.PHONE_LINE_DEAD && 
                MSG.SystemEvent <= (ushort)enuEventType.PHONE_LINE_ON_HOOK)
            {
                eventargs.Type = SystemEventType.Phone;

                if (MSG.SystemEvent == (ushort)enuEventType.PHONE_)
                {
                    eventargs.Value = "DEAD";
                    eventargs.Trouble = true;
                    eventargs.SendNotification = true;
                }
                else if (MSG.SystemEvent == (ushort)enuEventType.PHONE_LINE_RING)
                    eventargs.Value = "RING";
                else if (MSG.SystemEvent == (ushort)enuEventType.PHONE_LINE_OFF_HOOK)
                    eventargs.Value = "OFF HOOK";
                else if (MSG.SystemEvent == (ushort)enuEventType.PHONE_LINE_ON_HOOK)
                    eventargs.Value = "ON HOOK";

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= (ushort)enuEventType.AC_POWER_OFF && 
                MSG.SystemEvent <= (ushort)enuEventType.AC_POWER_RESTORED)
            {
                eventargs.Type = SystemEventType.AC;
                eventargs.SendNotification = true;

                if (MSG.SystemEvent == (ushort)enuEventType.AC_POWER_OFF)
                {
                    eventargs.Value = "OFF";
                    eventargs.Trouble = true;
                }
                else if (MSG.SystemEvent == (ushort)enuEventType.AC_POWER_RESTORED)
                    eventargs.Value = "RESTORED";

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= (ushort)enuEventType.BATTERY_LOW && 
                MSG.SystemEvent <= (ushort)enuEventType.BATTERY_OK)
            {
                eventargs.Type = SystemEventType.Battery;
                eventargs.SendNotification = true;

                if (MSG.SystemEvent == (ushort)enuEventType.BATTERY_LOW)
                {
                    eventargs.Value = "LOW";
                    eventargs.Trouble = true;
                }
                else if (MSG.SystemEvent == (ushort)enuEventType.BATTERY_OK)
                    eventargs.Value = "OK";

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= (ushort)enuEventType.DCM_TROUBLE && 
                MSG.SystemEvent <= (ushort)enuEventType.DCM_OK)
            {
                eventargs.Type = SystemEventType.DCM;

                eventargs.SendNotification = true;

                if (MSG.SystemEvent == (ushort)enuEventType.DCM_TROUBLE)
                {
                    eventargs.Value = "TROUBLE";
                    eventargs.Trouble = true;
                }
                else if (MSG.SystemEvent == (ushort)enuEventType.DCM_OK)
                    eventargs.Value = "OK";

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= (ushort)enuEventType.ENERGY_COST_LOW && 
                MSG.SystemEvent <= (ushort)enuEventType.ENERGY_COST_CRITICAL)
            {
                eventargs.Type = SystemEventType.EnergyCost;

                if (MSG.SystemEvent == (ushort)enuEventType.ENERGY_COST_LOW)
                    eventargs.Value = "LOW";
                else if (MSG.SystemEvent == (ushort)enuEventType.ENERGY_COST_MID)
                    eventargs.Value = "MID";
                else if (MSG.SystemEvent == (ushort)enuEventType.ENERGY_COST_HIGH)
                    eventargs.Value = "HIGH";
                else if (MSG.SystemEvent == (ushort)enuEventType.ENERGY_COST_CRITICAL)
                    eventargs.Value = "CRITICAL";

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= 782 && MSG.SystemEvent <= 787)
            {
                eventargs.Type = SystemEventType.Camera;
                eventargs.Value = (MSG.SystemEvent - 781).ToString();

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= 61440 && MSG.SystemEvent <= 64511)
            {
                eventargs.Type = SystemEventType.SwitchPress;
                int state = MSG.Data[1] - 240;
                int id = MSG.Data[2];

                eventargs.Value = "Unit: " + id + ", State: " + state;

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (MSG.SystemEvent >= 64512 && MSG.SystemEvent <= 65535)
            {
                eventargs.Type = SystemEventType.UPBLink;
                int state = MSG.Data[1] - 252;
                int id = MSG.Data[2];

                eventargs.Value = "Link: " + id + ", State: " + state;

                OnSystemStatus?.Invoke(this, eventargs);
            }
            else if (Global.verbose_unhandled)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < MSG.MessageLength; i++)
                    sb.Append(MSG.Data[i].ToString() + " ");
                log.Debug("Unhandled SystemEvent Raw: {raw}, Num: {num}", sb.ToString(), MSG.SystemEvent);

                int num = (MSG.MessageLength - 1) / 2;
                for (int i = 0; i < num; i++)
                {
                    log.Debug("Unhandled SystemEvent: " +
                        (int)MSG.Data[(i * 2) + 1] + " " + (int)MSG.Data[(i * 2) + 2] + ": " +
                        Convert.ToString(MSG.Data[(i * 2) + 1], 2).PadLeft(8, '0') + " " + Convert.ToString(MSG.Data[(i * 2) + 2], 2).PadLeft(8, '0'));
                }
            }
        }

        private void HandleUnsolicitedExtendedStatus(byte[] B)
        {
            clsOL2MsgExtendedStatus MSG = new clsOL2MsgExtendedStatus(Controller.Connection, B);

            switch (MSG.ObjectType)
            {
                case enuObjectType.Area:
                    for (byte i = 0; i < MSG.AreaCount(); i++)
                    {
                        Controller.Areas[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        OnAreaStatus?.Invoke(this, new AreaStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            Area = Controller.Areas[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                case enuObjectType.Auxillary:
                    for (byte i = 0; i < MSG.AuxStatusCount(); i++)
                    {
                        Controller.Zones[MSG.ObjectNumber(i)].CopyAuxExtendedStatus(MSG, i);
                        OnZoneStatus?.Invoke(this, new ZoneStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            Zone = Controller.Zones[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                case enuObjectType.Zone:
                    for (byte i = 0; i < MSG.ZoneStatusCount(); i++)
                    {
                        Controller.Zones[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        OnZoneStatus?.Invoke(this, new ZoneStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            Zone = Controller.Zones[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                case enuObjectType.Thermostat:
                    for (byte i = 0; i < MSG.ThermostatStatusCount(); i++)
                    {
                        lock (tstat_lock)
                        {
                            Controller.Thermostats[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);

                            OnThermostatStatus?.Invoke(this, new ThermostatStatusEventArgs()
                            {
                                ID = MSG.ObjectNumber(i),
                                Thermostat = Controller.Thermostats[MSG.ObjectNumber(i)],
                                Offline = Controller.Thermostats[MSG.ObjectNumber(i)].Temp == 0,
                                EventTimer = false
                            });

                            if (!tstats.ContainsKey(MSG.ObjectNumber(i)))
                                tstats.Add(MSG.ObjectNumber(i), DateTime.Now);
                            else
                                tstats[MSG.ObjectNumber(i)] = DateTime.Now;

                            if (Global.verbose_thermostat_timer)
                                log.Debug("Unsolicited status received for Thermostat {thermostatName}", 
                                    Controller.Thermostats[MSG.ObjectNumber(i)].Name);
                        }
                    }
                    break;
                case enuObjectType.Unit:
                    for (byte i = 0; i < MSG.UnitStatusCount(); i++)
                    {
                        Controller.Units[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        OnUnitStatus?.Invoke(this, new UnitStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            Unit = Controller.Units[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                case enuObjectType.Message:
                    for (byte i = 0; i < MSG.MessageCount(); i++)
                    {
                        Controller.Messages[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        OnMessageStatus?.Invoke(this, new MessageStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            Message = Controller.Messages[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                case enuObjectType.AccessControlLock:
                    for (byte i = 0; i < MSG.AccessControlLockCount(); i++)
                    {
                        Controller.AccessControlReaders[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        OnLockStatus?.Invoke(this, new LockStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            Reader = Controller.AccessControlReaders[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                case enuObjectType.AudioZone:
                    for (byte i = 0; i < MSG.AudioZoneStatusCount(); i++)
                    {
                        Controller.AudioZones[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        OnAudioZoneStatus?.Invoke(this, new AudioZoneStatusEventArgs()
                        {
                            ID = MSG.ObjectNumber(i),
                            AudioZone = Controller.AudioZones[MSG.ObjectNumber(i)]
                        });
                    }
                    break;
                default:
                    if (Global.verbose_unhandled)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (byte b in MSG.ToByteArray())
                            sb.Append(b.ToString() + " ");

                        log.Debug("Unhandled ExtendedStatus" + MSG.ObjectType.ToString() + " " + sb.ToString());
                    }
                    break;
            }
        }
        #endregion

        #region Thermostats
        static double ThermostatTimerInterval()
        {
            DateTime now = DateTime.Now;
            return ((60 - now.Second) * 1000 - now.Millisecond) + new TimeSpan(0, 0, 30).TotalMilliseconds;
        }

        private static DateTime RoundToMinute(DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
        }

        private void tstat_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (tstat_lock)
            {
                foreach (KeyValuePair<ushort, DateTime> tstat in tstats)
                {
                    // Poll every 4 minutes if no prior update
                    if (RoundToMinute(tstat.Value).AddMinutes(4) <= RoundToMinute(DateTime.Now) &&
                        (Controller.Connection.ConnectionState == enuOmniLinkConnectionState.Online ||
                        Controller.Connection.ConnectionState == enuOmniLinkConnectionState.OnlineSecure))
                    {
                        Controller.Connection.Send(new clsOL2MsgRequestExtendedStatus(Controller.Connection, 
                            enuObjectType.Thermostat, tstat.Key, tstat.Key), HandleRequestThermostatStatus);

                        if (Global.verbose_thermostat_timer)
                            log.Debug("Polling status for Thermostat {thermostatName}",
                                Controller.Thermostats[tstat.Key].Name);
                    }

                    // Log every minute if update within 5 minutes and connected
                    if (RoundToMinute(tstat.Value).AddMinutes(5) > RoundToMinute(DateTime.Now) &&
                        (Controller.Connection.ConnectionState == enuOmniLinkConnectionState.Online ||
                        Controller.Connection.ConnectionState == enuOmniLinkConnectionState.OnlineSecure))
                    {
                        OnThermostatStatus?.Invoke(this, new ThermostatStatusEventArgs()
                        {
                            ID = tstat.Key,
                            Thermostat = Controller.Thermostats[tstat.Key],
                            Offline = Controller.Thermostats[tstat.Key].Temp == 0,
                            EventTimer = true
                        });
                    }
                    else if (Global.verbose_thermostat_timer)
                        log.Warning("Not logging out of date status for Thermostat {thermostatName}",
                            Controller.Thermostats[tstat.Key].Name);
                }
            }

            tstat_timer.Interval = ThermostatTimerInterval();
            tstat_timer.Start();
        }

        private void HandleRequestThermostatStatus(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            clsOL2MsgExtendedStatus MSG = new clsOL2MsgExtendedStatus(Controller.Connection, B);

            for (byte i = 0; i < MSG.ThermostatStatusCount(); i++)
            {
                lock (tstat_lock)
                {
                    Controller.Thermostats[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);

                    if (!tstats.ContainsKey(MSG.ObjectNumber(i)))
                        tstats.Add(MSG.ObjectNumber(i), DateTime.Now);
                    else
                        tstats[MSG.ObjectNumber(i)] = DateTime.Now;

                    if (Global.verbose_thermostat_timer)
                        log.Debug("Polling status received for Thermostat {thermostatName}",
                            Controller.Thermostats[MSG.ObjectNumber(i)].Name);
                }
            }
        }
        #endregion

        #region Auxiliary
        private void HandleRequestAuxillaryStatus(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            clsOL2MsgExtendedStatus MSG = new clsOL2MsgExtendedStatus(Controller.Connection, B);

            for (byte i = 0; i < MSG.AuxStatusCount(); i++)
            {
                Controller.Zones[MSG.ObjectNumber(i)].CopyAuxExtendedStatus(MSG, i);
            }
        }
        #endregion
    }
}
