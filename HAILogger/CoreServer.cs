using HAI_Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Threading;

namespace HAILogger
{
    class CoreServer
    {
        private bool quitting = false;
        private bool terminate = false;

        // HAI Controller
        private clsHAC HAC = null;
        private DateTime retry = DateTime.MinValue;
        private List<string> alarms = new List<string>();

        // Thermostats
        private Dictionary<ushort, DateTime> tstats = new Dictionary<ushort, DateTime>();
        private System.Timers.Timer tstat_timer = new System.Timers.Timer();
        private object tstat_lock = new object();

        // Time Sync
        private System.Timers.Timer tsync_timer = new System.Timers.Timer();
        private DateTime tsync_check = DateTime.MinValue;

        // mySQL Database
        private OdbcConnection mysql_conn = null;
        private DateTime mysql_retry = DateTime.MinValue;
        private OdbcCommand mysql_command = null;
        private Queue<string> mysql_queue = new Queue<string>();
        private object mysql_lock = new object();

        public CoreServer()
        {
            Thread handler = new Thread(Server);
            handler.Start();
        }

        private void Server()
        {
            Global.event_log = "EventLog.txt";
            Global.event_source = "HAI Logger";

            if (string.IsNullOrEmpty(Global.dir_config))
                Global.dir_config = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            Settings.LoadSettings();

            Event.WriteInfo("CoreServer", "Starting up server");

            tstat_timer.Elapsed += tstat_timer_Elapsed;
            tstat_timer.AutoReset = false;

            tsync_timer.Elapsed += tsync_timer_Elapsed;
            tsync_timer.AutoReset = false;

            if (Global.mysql_logging)
            {
                Event.WriteInfo("DatabaseLogger", "Connecting to database");

                mysql_conn = new OdbcConnection(Global.mysql_connection);

                // Must make an initial connection
                if (!DBOpen())
                    Environment.Exit(1);
            }

            HAC = new clsHAC();

            WebService web = new WebService(HAC);

            if (Global.webapi_enabled)  
                web.Start();

            Connect();

            while (true)
            {
                // End gracefully when not logging or database queue empty
                if (quitting && (!Global.mysql_logging || DBQueueCount() == 0))
                    break;

                // Make sure controller connection is active
                if (HAC.Connection.ConnectionState == enuOmniLinkConnectionState.Offline &&
                    retry < DateTime.Now)
                {
                    Connect();
                }

                // Make sure database connection is active
                if (Global.mysql_logging && mysql_conn.State != ConnectionState.Open)
                {
                    // Nothing we can do if shutting down
                    if (quitting)
                        break;

                    if (mysql_retry < DateTime.Now)
                        DBOpen();

                    if (mysql_conn.State != ConnectionState.Open)
                    {
                        // Loop to prevent database queries from executing
                        Thread.Sleep(1000);
                        continue;
                    }
                }

                // Sleep when not logging or database queue empty
                if (!Global.mysql_logging || DBQueueCount() == 0)
                {
                    Thread.Sleep(1000);
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
                        Event.WriteWarn("DatabaseLogger", "Lost connection to database");
                    }
                    else
                    {
                        Event.WriteError("DatabaseLogger", "Error executing query\r\n" + ex.Message + "\r\n" + query);

                        // Prevent an endless loop from failed query
                        lock (mysql_lock)
                            mysql_queue.Dequeue();
                    }
                }
            }

            Event.WriteInfo("CoreServer", "Shutting down server");

            if (Global.webapi_enabled)
                web.Stop();

            Disconnect();
            HAC = null;

            if(Global.mysql_logging)
                DBClose();

            terminate = true;
        }

        private void Connected()
        {
            retry = DateTime.MinValue;

            GetNamedProperties();
            UnsolicitedNotifications(true);

            tstat_timer.Interval = ThermostatTimerInterval();
            tstat_timer.Start();

            if (Global.hai_time_sync)
            {
                tsync_check = DateTime.MinValue;

                tsync_timer.Interval = TimeTimerInterval();
                tsync_timer.Start();
            }
        }

        public void Shutdown()
        {
            quitting = true;

            while (!terminate)
                Thread.Sleep(100);

            Event.WriteInfo("CoreServer", "Shutdown completed");
        }

        #region Connection
        private void Connect()
        {
            if (HAC.Connection.ConnectionState == enuOmniLinkConnectionState.Offline)
            {
                retry = DateTime.Now.AddMinutes(1);

                HAC.Connection.NetworkAddress = Global.hai_address;
                HAC.Connection.NetworkPort = (ushort)Global.hai_port;
                HAC.Connection.ControllerKey = clsUtil.HexString2ByteArray(
                    String.Concat(Global.hai_key1, Global.hai_key2));

                HAC.PreferredNetworkProtocol = clsHAC.enuPreferredNetworkProtocol.TCP;
                HAC.Connection.ConnectionType = enuOmniLinkConnectionType.Network_TCP;

                HAC.Connection.Connect(HandleConnectStatus, HandleUnsolicitedPackets);
            }
        }

        private void Disconnect()
        {
            if (HAC.Connection.ConnectionState != enuOmniLinkConnectionState.Offline)
                HAC.Connection.Disconnect();
        }

        private void HandleConnectStatus(enuOmniLinkCommStatus CS)
        {
            switch (CS)
            {
                case enuOmniLinkCommStatus.NoReply:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: No Reply");
                    break;
                case enuOmniLinkCommStatus.UnrecognizedReply:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unrecognized Reply");
                    break;
                case enuOmniLinkCommStatus.UnsupportedProtocol:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unsupported Protocol");
                    break;
                case enuOmniLinkCommStatus.ClientSessionTerminated:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Client Session Terminated");
                    break;
                case enuOmniLinkCommStatus.ControllerSessionTerminated:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Controller Session Terminated");
                    break;
                case enuOmniLinkCommStatus.CannotStartNewSession:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Cannot Start New Session");
                    break;
                case enuOmniLinkCommStatus.LoginFailed:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Login Failed");
                    break;
                case enuOmniLinkCommStatus.UnableToOpenSocket:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unable To Open Socket");
                    break;
                case enuOmniLinkCommStatus.UnableToConnect:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unable To Connect");
                    break;
                case enuOmniLinkCommStatus.SocketClosed:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Socket Closed");
                    break;
                case enuOmniLinkCommStatus.UnexpectedError:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unexpected Error");
                    break;
                case enuOmniLinkCommStatus.UnableToCreateSocket:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unable To Create Socket");
                    break;
                case enuOmniLinkCommStatus.Retrying:
                    Event.WriteWarn("CoreServer", "CONNECTION STATUS: Retrying");
                    break;
                case enuOmniLinkCommStatus.Connected:
                    IdentifyController();
                    break;
                case enuOmniLinkCommStatus.Connecting:
                    Event.WriteInfo("CoreServer", "CONNECTION STATUS: Connecting");
                    break;
                case enuOmniLinkCommStatus.Disconnected:
                    Event.WriteInfo("CoreServer", "CONNECTION STATUS: Disconnected");
                    break;
                case enuOmniLinkCommStatus.InterruptedFunctionCall:
                    if(!quitting)
                        Event.WriteError("CoreServer", "CONNECTION STATUS: Interrupted Function Call");
                    break;
                case enuOmniLinkCommStatus.PermissionDenied:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Permission Denied");
                    break;
                case enuOmniLinkCommStatus.BadAddress:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Bad Address");
                    break;
                case enuOmniLinkCommStatus.InvalidArgument:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Invalid Argument");
                    break;
                case enuOmniLinkCommStatus.TooManyOpenFiles:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Too Many Open Files");
                    break;
                case enuOmniLinkCommStatus.ResourceTemporarilyUnavailable:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Resource Temporarily Unavailable");
                    break;
                case enuOmniLinkCommStatus.OperationNowInProgress:
                    Event.WriteWarn("CoreServer", "CONNECTION STATUS: Operation Now In Progress");
                    break;
                case enuOmniLinkCommStatus.OperationAlreadyInProgress:
                    Event.WriteWarn("CoreServer", "CONNECTION STATUS: Operation Already In Progress");
                    break;
                case enuOmniLinkCommStatus.SocketOperationOnNonSocket:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Socket Operation On Non Socket");
                    break;
                case enuOmniLinkCommStatus.DestinationAddressRequired:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Destination Address Required");
                    break;
                case enuOmniLinkCommStatus.MessgeTooLong:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Message Too Long");
                    break;
                case enuOmniLinkCommStatus.WrongProtocolType:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Wrong Protocol Type");
                    break;
                case enuOmniLinkCommStatus.BadProtocolOption:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Bad Protocol Option");
                    break;
                case enuOmniLinkCommStatus.ProtocolNotSupported:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Protocol Not Supported");
                    break;
                case enuOmniLinkCommStatus.SocketTypeNotSupported:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Socket Type Not Supported");
                    break;
                case enuOmniLinkCommStatus.OperationNotSupported:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Operation Not Supported");
                    break;
                case enuOmniLinkCommStatus.ProtocolFamilyNotSupported:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Protocol Family Not Supported");
                    break;
                case enuOmniLinkCommStatus.AddressFamilyNotSupported:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Address Family Not Supported");
                    break;
                case enuOmniLinkCommStatus.AddressInUse:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Address In Use");
                    break;
                case enuOmniLinkCommStatus.AddressNotAvailable:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Address Not Available");
                    break;
                case enuOmniLinkCommStatus.NetworkIsDown:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Network Is Down");
                    break;
                case enuOmniLinkCommStatus.NetworkIsUnreachable:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Network Is Unreachable");
                    break;
                case enuOmniLinkCommStatus.NetworkReset:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Network Reset");
                    break;
                case enuOmniLinkCommStatus.ConnectionAborted:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Connection Aborted");
                    break;
                case enuOmniLinkCommStatus.ConnectionResetByPeer:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Connection Reset By Peer");
                    break;
                case enuOmniLinkCommStatus.NoBufferSpaceAvailable:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: No Buffer Space Available");
                    break;
                case enuOmniLinkCommStatus.AlreadyConnected:
                    Event.WriteWarn("CoreServer", "CONNECTION STATUS: Already Connected");
                    break;
                case enuOmniLinkCommStatus.NotConnected:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Not Connected");
                    break;
                case enuOmniLinkCommStatus.CannotSendAfterShutdown:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Cannot Send After Shutdown");
                    break;
                case enuOmniLinkCommStatus.ConnectionTimedOut:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Connection Timed Out");
                    break;
                case enuOmniLinkCommStatus.ConnectionRefused:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Connection Refused");
                    break;
                case enuOmniLinkCommStatus.HostIsDown:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Host Is Down");
                    break;
                case enuOmniLinkCommStatus.HostUnreachable:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Host Unreachable");
                    break;
                case enuOmniLinkCommStatus.TooManyProcesses:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Too Many Processes");
                    break;
                case enuOmniLinkCommStatus.NetworkSubsystemIsUnavailable:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Network Subsystem Is Unavailable");
                    break;
                case enuOmniLinkCommStatus.UnsupportedVersion:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Unsupported Version");
                    break;
                case enuOmniLinkCommStatus.NotInitialized:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Not Initialized");
                    break;
                case enuOmniLinkCommStatus.ShutdownInProgress:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Shutdown In Progress");
                    break;
                case enuOmniLinkCommStatus.ClassTypeNotFound:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Class Type Not Found");
                    break;
                case enuOmniLinkCommStatus.HostNotFound:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Host Not Found");
                    break;
                case enuOmniLinkCommStatus.HostNotFoundTryAgain:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Host Not Found Try Again");
                    break;
                case enuOmniLinkCommStatus.NonRecoverableError:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: Non Recoverable Error");
                    break;
                case enuOmniLinkCommStatus.NoDataOfRequestedType:
                    Event.WriteError("CoreServer", "CONNECTION STATUS: No Data Of Requested Type");
                    break;
                default:
                    break;
            }
        }

        private void IdentifyController()
        {
            if (HAC.Connection.ConnectionState == enuOmniLinkConnectionState.Online ||
                HAC.Connection.ConnectionState == enuOmniLinkConnectionState.OnlineSecure)
            {
                HAC.Connection.Send(new clsOL2MsgRequestSystemInformation(HAC.Connection), HandleIdentifyController);
            }
        }

        private void HandleIdentifyController(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            if ((B.Length > 3) && (B[2] == (byte)enuOmniLink2MessageType.SystemInformation))
            {
                clsOL2MsgSystemInformation MSG = new clsOL2MsgSystemInformation(HAC.Connection, B);
                if (HAC.Model == MSG.ModelNumber)
                {
                    HAC.CopySystemInformation(MSG);
                    Event.WriteInfo("CoreServer", "CONTROLLER IS: " + HAC.GetModelText() + " (" + HAC.GetVersionText() + ")");

                    Connected();
                    return;
                }

                Event.WriteError("CoreServer", "Model does not match file");
                HAC.Connection.Disconnect();
            }
        }
        #endregion

        #region Names
        private void GetNamedProperties()
        {
            Event.WriteInfo("CoreServer", "Retrieving named units");

            GetNextNamed(enuObjectType.Area, 0);
            Thread.Sleep(100);
            GetNextNamed(enuObjectType.Zone, 0);
            Thread.Sleep(100);
            GetNextNamed(enuObjectType.Thermostat, 0);
            Thread.Sleep(100);
            GetNextNamed(enuObjectType.Unit, 0);
            Thread.Sleep(100);
            GetNextNamed(enuObjectType.Message, 0);
            Thread.Sleep(100);
            GetNextNamed(enuObjectType.Button, 0);
            Thread.Sleep(100);
        }

        private void GetNextNamed(enuObjectType type, int ix)
        {
            clsOL2MsgRequestProperties MSG = new clsOL2MsgRequestProperties(HAC.Connection);
            MSG.ObjectType = type;
            MSG.IndexNumber = (UInt16)ix;
            MSG.RelativeDirection = 1;  // next object after IndexNumber
            MSG.Filter1 = 1;  // (0=Named or Unnamed, 1=Named, 2=Unnamed).
            MSG.Filter2 = 0;  // Any Area
            MSG.Filter3 = 0;  // Any Room
            HAC.Connection.Send(MSG, HandleNamedPropertiesResponse);
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
                        
                        break;
                    case enuOmniLink2MessageType.Properties:

                        clsOL2MsgProperties MSG = new clsOL2MsgProperties(HAC.Connection, B);

                        switch (MSG.ObjectType)
                        {
                            case enuObjectType.Area:
                                HAC.Areas.CopyProperties(MSG);
                                break;                            
                            case enuObjectType.Zone:
                                HAC.Zones.CopyProperties(MSG);
                                break;
                            case enuObjectType.Thermostat:
                                HAC.Thermostats.CopyProperties(MSG);

                                if (!tstats.ContainsKey(MSG.ObjectNumber))
                                    tstats.Add(MSG.ObjectNumber, DateTime.MinValue);
                                else
                                    tstats[MSG.ObjectNumber] = DateTime.MinValue;

                                HAC.Connection.Send(new clsOL2MsgRequestExtendedStatus(HAC.Connection, enuObjectType.Thermostat, MSG.ObjectNumber, MSG.ObjectNumber), HandleRequestThermostatStatus);
                                Event.WriteVerbose("ThermostatTimer", "Added to watch list " + HAC.Thermostats[MSG.ObjectNumber].Name);
                                break;
                            case enuObjectType.Unit:
                                HAC.Units.CopyProperties(MSG);
                                break;
                            case enuObjectType.Message:
                                HAC.Messages.CopyProperties(MSG);
                                break;
                            case enuObjectType.Button:
                                HAC.Buttons.CopyProperties(MSG);
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
            Event.WriteInfo("CoreServer", "Unsolicited notifications " + (enable ? "enabled" : "disabled"));
            HAC.Connection.Send(new clsOL2EnableNotifications(HAC.Connection, enable), null);
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

                if(Global.verbose_unhandled && !handled)
                    Event.WriteVerbose("CoreServer", "Unhandled notification: " + ((enuOmniLink2MessageType)B[2]).ToString());
            }

            return true;
        }

        private void HandleUnsolicitedSystemEvent(byte[] B)
        {
            clsOL2SystemEvent MSG = new clsOL2SystemEvent(HAC.Connection, B);

            enuEventType type;
            string value = string.Empty;
            bool alert = false;

            if (MSG.SystemEvent >= 1 && MSG.SystemEvent <= 255)
            {
                type = enuEventType.USER_MACRO_BUTTON;
                value = ((int)MSG.SystemEvent).ToString() + " " + HAC.Buttons[MSG.SystemEvent].Name;
                
                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 768 && MSG.SystemEvent <= 771)
            {
                type = enuEventType.PHONE_;

                if (MSG.SystemEvent == 768)
                {
                    value = "DEAD";
                    alert = true;
                }
                else if (MSG.SystemEvent == 769)
                    value = "RING";
                else if (MSG.SystemEvent == 770)
                    value = "OFF HOOK";
                else if (MSG.SystemEvent == 771)
                    value = "ON HOOK";

                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 772 && MSG.SystemEvent <= 773)
            {
                type = enuEventType.AC_POWER_;
                alert = true;

                if (MSG.SystemEvent == 772)
                    value = "OFF";
                else if (MSG.SystemEvent == 773)
                    value = "RESTORED";

                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 774 && MSG.SystemEvent <= 775)
            {
                type = enuEventType.BATTERY_;
                alert = true;

                if (MSG.SystemEvent == 774)
                    value = "LOW";
                else if (MSG.SystemEvent == 775)
                    value = "OK";

                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 776 && MSG.SystemEvent <= 777)
            {
                type = enuEventType.DCM_;
                alert = true;

                if (MSG.SystemEvent == 776)
                    value = "TROUBLE";
                else if (MSG.SystemEvent == 777)
                    value = "OK";

                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 778 && MSG.SystemEvent <= 781)
            {
                type = enuEventType.ENERGY_COST_;

                if (MSG.SystemEvent == 778)
                    value = "LOW";
                else if (MSG.SystemEvent == 779)
                    value = "MID";
                else if (MSG.SystemEvent == 780)
                    value = "HIGH";
                else if (MSG.SystemEvent == 781)
                    value = "CRITICAL";

                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 782 && MSG.SystemEvent <= 787)
            {
                type = enuEventType.CAMERA;
                value = (MSG.SystemEvent - 781).ToString();

                LogEventStatus(type, value, alert);
            }
            else if (MSG.SystemEvent >= 61440 && MSG.SystemEvent <= 64511)
            {
                type = enuEventType.SWITCH_PRESS;
                int state = (int)MSG.Data[1] - 240;
                int id = (int)MSG.Data[2];

                LogEventStatus(type, "Unit: " + id + ", State: " + state, alert);
            }
            else if (MSG.SystemEvent >= 64512 && MSG.SystemEvent <= 65535)
            {
                type = enuEventType.UPB_LINK;
                int state = (int)MSG.Data[1] - 252;
                int id = (int)MSG.Data[2];

                LogEventStatus(type, "Link: " + id + ", State: " + state, alert);
            }
            else if (Global.verbose_unhandled)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < MSG.MessageLength; i++)
                    sb.Append(MSG.Data[i].ToString() + " ");
                Event.WriteVerbose("SystemEvent", "Unhandled Raw: " + sb.ToString() + "Num: " + MSG.SystemEvent);

                int num = ((int)MSG.MessageLength - 1) / 2;
                for (int i = 0; i < num; i++)
                {
                    Event.WriteVerbose("SystemEvent", "Unhandled: " +
                        (int)MSG.Data[(i * 2) + 1] + " " + (int)MSG.Data[(i * 2) + 2] + ": " +
                        Convert.ToString(MSG.Data[(i * 2) + 1], 2).PadLeft(8, '0') + " " + Convert.ToString(MSG.Data[(i * 2) + 2], 2).PadLeft(8, '0'));
                }
            }
        }

        private void HandleUnsolicitedExtendedStatus(byte[] B)
        {
            clsOL2MsgExtendedStatus MSG = new clsOL2MsgExtendedStatus(HAC.Connection, B);

            switch (MSG.ObjectType)
            {
                case enuObjectType.Area:
                    for (byte i = 0; i < MSG.AreaCount(); i++)
                    {
                        HAC.Areas[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        LogAreaStatus(MSG.ObjectNumber(i));

                        WebNotification.Send("area", Helper.Serialize<AreaContract>(Helper.ConvertArea(
                            MSG.ObjectNumber(i), HAC.Areas[MSG.ObjectNumber(i)])));
                    }
                    break;
                case enuObjectType.Zone:
                    for (byte i = 0; i < MSG.ZoneStatusCount(); i++)
                    {
                        HAC.Zones[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        LogZoneStatus(MSG.ObjectNumber(i));

                        switch (HAC.Zones[MSG.ObjectNumber(i)].ZoneType)
                        {
                            case enuZoneType.EntryExit:
                            case enuZoneType.X2EntryDelay:
                            case enuZoneType.X4EntryDelay:
                            case enuZoneType.Perimeter:
                                WebNotification.Send("contact", Helper.Serialize<ZoneContract>(Helper.ConvertZone(
                                    MSG.ObjectNumber(i), HAC.Zones[MSG.ObjectNumber(i)])));
                                break;
                            case enuZoneType.AwayInt:
                                WebNotification.Send("motion", Helper.Serialize<ZoneContract>(Helper.ConvertZone(
                                    MSG.ObjectNumber(i), HAC.Zones[MSG.ObjectNumber(i)])));
                                break;
                            case enuZoneType.Water:
                                WebNotification.Send("water", Helper.Serialize<ZoneContract>(Helper.ConvertZone(
                                    MSG.ObjectNumber(i), HAC.Zones[MSG.ObjectNumber(i)])));
                                break;
                            case enuZoneType.Fire:
                                WebNotification.Send("smoke", Helper.Serialize<ZoneContract>(Helper.ConvertZone(
                                    MSG.ObjectNumber(i), HAC.Zones[MSG.ObjectNumber(i)])));
                                break;
                            case enuZoneType.Gas:
                                WebNotification.Send("co", Helper.Serialize<ZoneContract>(Helper.ConvertZone(
                                    MSG.ObjectNumber(i), HAC.Zones[MSG.ObjectNumber(i)])));
                                break;
                        }
                    }
                    break;
                case enuObjectType.Thermostat:
                    for (byte i = 0; i < MSG.ThermostatStatusCount(); i++)
                    {
                        lock (tstat_lock)
                        {
                            HAC.Thermostats[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);

                            if (!tstats.ContainsKey(MSG.ObjectNumber(i)))
                                tstats.Add(MSG.ObjectNumber(i), DateTime.Now);
                            else
                                tstats[MSG.ObjectNumber(i)] = DateTime.Now;

                            if (Global.verbose_thermostat_timer)
                                Event.WriteVerbose("ThermostatTimer", "Unsolicited status received for " + HAC.Thermostats[MSG.ObjectNumber(i)].Name);

                            WebNotification.Send("thermostat", Helper.Serialize<ThermostatContract>(Helper.ConvertThermostat(
                                MSG.ObjectNumber(i), HAC.Thermostats[MSG.ObjectNumber(i)])));
                        }
                    }
                    break;
                case enuObjectType.Unit:
                    for (byte i = 0; i < MSG.UnitStatusCount(); i++)
                    {
                        HAC.Units[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        LogUnitStatus(MSG.ObjectNumber(i));

                        WebNotification.Send("unit", Helper.Serialize<UnitContract>(Helper.ConvertUnit(
                            MSG.ObjectNumber(i), HAC.Units[MSG.ObjectNumber(i)])));
                    }
                    break;
                case enuObjectType.Message:
                    for (byte i = 0; i < MSG.MessageCount(); i++)
                    {
                        HAC.Messages[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);
                        LogMessageStatus(MSG.ObjectNumber(i));
                    }
                    break;
                default:
                    if (Global.verbose_unhandled)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (byte b in MSG.ToByteArray())
                            sb.Append(b.ToString() + " ");
                        Event.WriteVerbose("ExtendedStatus", "Unhandled " + MSG.ObjectType.ToString() + " " + sb.ToString());
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
                    if (RoundToMinute(tstat.Value).AddMinutes(4) <= RoundToMinute(DateTime.Now))
                    {
                        HAC.Connection.Send(new clsOL2MsgRequestExtendedStatus(HAC.Connection, enuObjectType.Thermostat, tstat.Key, tstat.Key), HandleRequestThermostatStatus);

                        if(Global.verbose_thermostat_timer)
                            Event.WriteVerbose("ThermostatTimer", "Polling status for " + HAC.Thermostats[tstat.Key].Name);
                    }

                    // Log every minute if update within 5 minutes and connected
                    if (RoundToMinute(tstat.Value).AddMinutes(5) > RoundToMinute(DateTime.Now) && 
                        (HAC.Connection.ConnectionState == enuOmniLinkConnectionState.Online ||
                        HAC.Connection.ConnectionState == enuOmniLinkConnectionState.OnlineSecure))
                    {
                        if (HAC.Thermostats[tstat.Key].Temp > 0)
                            LogThermostatStatus(tstat.Key);
                        else if (Global.verbose_thermostat_timer)
                            Event.WriteWarn("ThermostatTimer", "Not logging unknown temp for " + HAC.Thermostats[tstat.Key].Name);
                    }
                    else if (Global.verbose_thermostat_timer)
                        Event.WriteWarn("ThermostatTimer", "Not logging out of date status for " + HAC.Thermostats[tstat.Key].Name);
                }
            }

            tstat_timer.Interval = ThermostatTimerInterval();
            tstat_timer.Start();
        }

        private void HandleRequestThermostatStatus(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            clsOL2MsgExtendedStatus MSG = new clsOL2MsgExtendedStatus(HAC.Connection, B);

            for (byte i = 0; i < MSG.ThermostatStatusCount(); i++)
            {
                lock (tstat_lock)
                {
                    HAC.Thermostats[MSG.ObjectNumber(i)].CopyExtendedStatus(MSG, i);

                    if (!tstats.ContainsKey(MSG.ObjectNumber(i)))
                        tstats.Add(MSG.ObjectNumber(i), DateTime.Now);
                    else
                        tstats[MSG.ObjectNumber(i)] = DateTime.Now;

                    if (Global.verbose_thermostat_timer)
                        Event.WriteVerbose("ThermostatTimer", "Polling status received for " + HAC.Thermostats[MSG.ObjectNumber(i)].Name);
                }
            }
        }
        #endregion

        #region Time Sync
        static double TimeTimerInterval()
        {
            DateTime now = DateTime.Now;
            return ((60 - now.Second) * 1000 - now.Millisecond);
        }

        private void tsync_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (tsync_check.AddMinutes(Global.hai_time_interval) < DateTime.Now)
                HAC.Connection.Send(new clsOL2MsgRequestSystemStatus(HAC.Connection), HandleRequestSystemStatus);      

            tsync_timer.Interval = TimeTimerInterval();
            tsync_timer.Start();
        }

        private void HandleRequestSystemStatus(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            tsync_check = DateTime.Now;

            clsOL2MsgSystemStatus MSG = new clsOL2MsgSystemStatus(HAC.Connection, B);

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
                Event.WriteWarn("TimeSyncTimer", "Controller time could not be parsed", true);

                DateTime now = DateTime.Now;
                HAC.Connection.Send(new clsOL2MsgSetTime(HAC.Connection, (byte)(now.Year % 100), (byte)now.Month, (byte)now.Day, (byte)now.DayOfWeek,
                    (byte)now.Hour, (byte)now.Minute, (byte)(now.IsDaylightSavingTime() ? 1 : 0)), HandleSetTime);

                return;
            }    

            double adj = (DateTime.Now - time).Duration().TotalSeconds;

            if (adj > Global.hai_time_drift)
            {
                Event.WriteWarn("TimeSyncTimer", "Controller time " + time.ToString("MM/dd/yyyy HH:mm:ss") + " out of sync by " + adj + " seconds", true);

                DateTime now = DateTime.Now;
                HAC.Connection.Send(new clsOL2MsgSetTime(HAC.Connection, (byte)(now.Year % 100), (byte)now.Month, (byte)now.Day, (byte)now.DayOfWeek, 
                    (byte)now.Hour, (byte)now.Minute, (byte)(now.IsDaylightSavingTime() ? 1 : 0)), HandleSetTime);
            }
        }

        private void HandleSetTime(clsOmniLinkMessageQueueItem M, byte[] B, bool Timeout)
        {
            if (Timeout)
                return;

            Event.WriteVerbose("TimeSyncTimer", "Controller time has been successfully set");
        }
        #endregion

        #region Logging
        private void LogEventStatus(enuEventType type, string value, bool alert)
        {
            DBQueue(@"
                INSERT INTO log_events (timestamp, name, status)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + type.ToString() + "','" + value + "')");

            if (alert)
            {
                Event.WriteWarn("SystemEvent", type.ToString() + " " + value);
                Prowl.Notify("SystemEvent", type.ToString() + " " + value);
            }

            if (Global.verbose_event)
                Event.WriteVerbose("SystemEvent", type.ToString() + " " + value);
        }

        private void LogAreaStatus(ushort id)
        {
            clsArea unit = HAC.Areas[id];

            // Alarm notifcation
            if (unit.AreaFireAlarmText != "OK")
            {
                Event.WriteAlarm("AreaStatus", "FIRE " + unit.Name + " " + unit.AreaFireAlarmText);
                Prowl.Notify("ALARM", "FIRE " + unit.Name + " " + unit.AreaFireAlarmText, ProwlPriority.Emergency);

                if(!alarms.Contains("FIRE" + id))
                    alarms.Add("FIRE" + id);
            }
            else if(alarms.Contains("FIRE" + id))
            {
                Event.WriteAlarm("AreaStatus", "CLEARED - FIRE " + unit.Name + " " + unit.AreaFireAlarmText);
                Prowl.Notify("ALARM CLEARED", "FIRE " + unit.Name + " " + unit.AreaFireAlarmText, ProwlPriority.High);

                alarms.Remove("FIRE" + id);
            }

            if (unit.AreaBurglaryAlarmText != "OK")
            {
                Event.WriteAlarm("AreaStatus", "BURGLARY " + unit.Name + " " + unit.AreaBurglaryAlarmText);
                Prowl.Notify("ALARM", "BURGLARY " + unit.Name + " " + unit.AreaBurglaryAlarmText, ProwlPriority.Emergency);

                if (!alarms.Contains("BURGLARY" + id))
                    alarms.Add("BURGLARY" + id);
            }
            else if (alarms.Contains("BURGLARY" + id))
            {
                Event.WriteAlarm("AreaStatus", "CLEARED - BURGLARY " + unit.Name + " " + unit.AreaBurglaryAlarmText);
                Prowl.Notify("ALARM CLEARED", "BURGLARY " + unit.Name + " " + unit.AreaBurglaryAlarmText, ProwlPriority.High);

                alarms.Remove("BURGLARY" + id);
            }

            if (unit.AreaAuxAlarmText != "OK")
            {
                Event.WriteAlarm("AreaStatus", "AUX " + unit.Name + " " + unit.AreaAuxAlarmText);
                Prowl.Notify("ALARM", "AUX " + unit.Name + " " + unit.AreaAuxAlarmText, ProwlPriority.Emergency);

                if (!alarms.Contains("AUX" + id))
                    alarms.Add("AUX" + id);
            }
            else if (alarms.Contains("AUX" + id))
            {
                Event.WriteAlarm("AreaStatus", "CLEARED - AUX " + unit.Name + " " + unit.AreaAuxAlarmText);
                Prowl.Notify("ALARM CLEARED", "AUX " + unit.Name + " " + unit.AreaAuxAlarmText, ProwlPriority.High);

                alarms.Remove("AUX" + id);
            }

            if (unit.AreaDuressAlarmText != "OK")
            {
                Event.WriteAlarm("AreaStatus", "DURESS " + unit.Name + " " + unit.AreaDuressAlarmText);
                Prowl.Notify("ALARM", "DURESS " + unit.Name + " " + unit.AreaDuressAlarmText, ProwlPriority.Emergency);

                if (!alarms.Contains("DURESS" + id))
                    alarms.Add("DURESS" + id);
            }
            else if (alarms.Contains("DURESS" + id))
            {
                Event.WriteAlarm("AreaStatus", "CLEARED - DURESS " + unit.Name + " " + unit.AreaDuressAlarmText);
                Prowl.Notify("ALARM CLEARED", "DURESS " + unit.Name + " " + unit.AreaDuressAlarmText, ProwlPriority.High);

                alarms.Remove("DURESS" + id);
            }

            string status = unit.ModeText();

            if (unit.ExitTimer > 0)
                status = "ARMING " + status;

            if (unit.EntryTimer > 0)
                status = "TRIPPED " + status;

            DBQueue(@"
            INSERT INTO log_areas (timestamp, id, name, 
                fire, police, auxiliary, 
                duress, security)
            VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + id.ToString() + "','" + unit.Name + "','" +
                    unit.AreaFireAlarmText + "','" + unit.AreaBurglaryAlarmText + "','" + unit.AreaAuxAlarmText + "','" +
                    unit.AreaDuressAlarmText + "','" + status + "')");

            if(Global.verbose_area)
                Event.WriteVerbose("AreaStatus", id + " " + unit.Name + ", Status: " + status);

            if(unit.LastMode != unit.AreaMode)
                Prowl.Notify("Security", unit.Name + " " + unit.ModeText());
        }

        private void LogZoneStatus(ushort id)
        {
            clsZone unit = HAC.Zones[id];

            DBQueue(@"
                INSERT INTO log_zones (timestamp, id, name, status)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + id.ToString() + "','" + unit.Name + "','" + unit.StatusText() + "')");
            
            if(Global.verbose_zone)
                Event.WriteVerbose("ZoneStatus", id + " " + unit.Name + ", Status: " + unit.StatusText());
        }

        private void LogThermostatStatus(ushort id)
        {
            clsThermostat unit = HAC.Thermostats[id];

            int temp, heat, cool, humidity, humidify, dehumidify;

            Int32.TryParse(unit.TempText(), out temp);
            Int32.TryParse(unit.HeatSetpointText(), out heat);
            Int32.TryParse(unit.CoolSetpointText(), out cool);
            Int32.TryParse(unit.HumidityText(), out humidity);
            Int32.TryParse(unit.HumidifySetpointText(), out humidify);
            Int32.TryParse(unit.DehumidifySetpointText(), out dehumidify);

            DBQueue(@"
                INSERT INTO log_thermostats (timestamp, id, name, 
                    status, temp, heat, cool, 
                    humidity, humidify, dehumidify,
                    mode, fan, hold)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "','" + id.ToString() + "','" + unit.Name + "','" +
                    unit.HorC_StatusText() + "','" + temp.ToString() + "','" + heat + "','" + cool + "','" +
                    humidity + "','" + humidify + "','" + dehumidify + "','" +
                    unit.ModeText() + "','" + unit.FanModeText() + "','" + unit.HoldStatusText() + "')");

            if(Global.verbose_thermostat)
                Event.WriteVerbose("ThermostatStatus", id + " " + unit.Name +
                    ", Status: " + unit.TempText() + " " + unit.HorC_StatusText() + 
                    ", Heat: " + unit.HeatSetpointText() +
                    ", Cool: " + unit.CoolSetpointText() + 
                    ", Mode: " + unit.ModeText() + 
                    ", Fan: " + unit.FanModeText() + 
                    ", Hold: " + unit.HoldStatusText());
        }

        private void LogUnitStatus(ushort id)
        {
            clsUnit unit = HAC.Units[id];

            string status = unit.StatusText;

            if (unit.Status == 100 && unit.StatusTime == 0)
                status = "OFF";
            else if (unit.Status == 200 && unit.StatusTime == 0)
                status = "ON";

            DBQueue(@"
                INSERT INTO log_units (timestamp, id, name, 
                    status, statusvalue, statustime)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + id.ToString() + "','" + unit.Name + "','" +
                    status + "','" + unit.Status + "','" + unit.StatusTime + "')");

            if(Global.verbose_unit)
                Event.WriteVerbose("UnitStatus", id + " " + unit.Name + ", Status: " +  status);
        }

        private void LogMessageStatus(ushort id)
        {
            clsMessage unit = HAC.Messages[id];

            DBQueue(@"
                INSERT INTO log_messages (timestamp, id, name, status)
                VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "','" + id.ToString() + "','" + unit.Name + "','" + unit.StatusText() + "')");

            if(Global.verbose_message)
                Event.WriteVerbose("MessageStatus", unit.Name + ", " + unit.StatusText());

            if(Global.prowl_messages)
                Prowl.Notify("Message", id + " " + unit.Name + ", " + unit.StatusText());
        }

        #endregion 

        #region Database
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
                Event.WriteError("DatabaseLogger", "Failed to connect to database\r\n" + ex.Message);
                mysql_retry = DateTime.Now.AddMinutes(1);
                return false;
            }

            return true;
        }

        public void DBClose()
        {
            if(mysql_conn.State != ConnectionState.Closed)
                mysql_conn.Close();
        }

        public void DBQueue(string query)
        {
            if (!Global.mysql_logging)
                return;

            lock(mysql_lock)
                mysql_queue.Enqueue(query);
        }

        private int DBQueueCount()
        {
            int count;
            lock (mysql_lock)
                count = mysql_queue.Count;

            return count;
        }
        #endregion
    }
}
