using HAI_Shared;
using System;

namespace OmniLinkBridge.OmniLink
{
    public class ThermostatStatusEventArgs : EventArgs
    {
        public ushort ID { get; set; }
        public clsThermostat Thermostat { get; set; }

        /// <summary>
        /// Set to true when fired by thermostat polling
        /// </summary>
        public bool EventTimer { get; set; }
    }
}
