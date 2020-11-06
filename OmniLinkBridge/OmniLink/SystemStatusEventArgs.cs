using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class SystemStatusEventArgs : EventArgs
    {
        public SystemEventType Type { get; set; }
        public string Value { get; set; }
        public bool Trouble { get; set; }
        public bool SendNotification { get; set; }
    }
}
