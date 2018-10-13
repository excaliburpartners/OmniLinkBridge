using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class ThermostatStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsThermostat Thermostat { get; set; }
        public bool EventTimer { get; set; }
    }
}
