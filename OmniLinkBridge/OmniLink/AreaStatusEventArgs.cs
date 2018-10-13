using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class AreaStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsArea Area { get; set; }
    }
}
