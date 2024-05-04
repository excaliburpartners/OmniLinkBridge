using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class AudioZoneStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsAudioZone AudioZone { get; set; }
    }
}
