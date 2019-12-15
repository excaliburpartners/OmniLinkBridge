using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class ButtonStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsButton Button { get; set; }
    }
}
