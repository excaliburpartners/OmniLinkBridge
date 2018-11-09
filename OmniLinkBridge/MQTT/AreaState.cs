using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.MQTT
{
    public class AreaState
    {
        public string mode { get; set; }
        public bool arming { get; set; }
        public bool burglary_alarm { get; set; }
        public bool fire_alarm { get; set; }
        public bool gas_alarm { get; set; }
        public bool auxiliary_alarm { get; set; }
        public bool freeze_alarm { get; set; }
        public bool water_alarm { get; set; }
        public bool duress_alarm { get; set; }
        public bool temperature_alarm { get; set; }
    }
}
