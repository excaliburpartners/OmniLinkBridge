using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class ZoneStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsZone Zone { get; set; }
    }
}
