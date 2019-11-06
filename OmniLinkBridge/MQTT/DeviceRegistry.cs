using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.MQTT
{
    public class DeviceRegistry
    {
        public string identifiers { get; set; }
        public string name { get; set; }
        public string sw_version { get; set; }
        public string model { get; set; }
        public string manufacturer { get; set; }
    }
}
