using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class ThermostatStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsThermostat Thermostat { get; set; }

        /// <summary>
        /// Set to true when thermostat is offline, indicated by a temperature of 0
        /// </summary>
        public bool Offline { get; set; }

        /// <summary>
        /// Set to true when fired by thermostat polling
        /// </summary>
        public bool EventTimer { get; set; }
    }
}
