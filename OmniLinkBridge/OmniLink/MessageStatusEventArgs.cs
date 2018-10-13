using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class MessageStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsMessage Message { get; set; }
    }
}
