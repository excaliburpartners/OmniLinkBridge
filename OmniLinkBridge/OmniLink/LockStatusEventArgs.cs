using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class LockStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsAccessControlReader Reader { get; set; }
    }
}
