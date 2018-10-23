using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.MQTT
{
    public class Device
    {
        public string name { get; set; }

        public string state_topic { get; set; }

        public string availability_topic { get; set; } = $"{Global.mqtt_prefix}/status";
    }
}
