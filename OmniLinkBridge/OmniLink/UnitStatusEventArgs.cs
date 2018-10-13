using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class UnitStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsUnit Unit { get; set; }
    }
}
