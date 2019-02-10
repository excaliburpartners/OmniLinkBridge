using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.MQTT
{
    public class Alarm : Device
    {
        public string command_topic { get; set; }

        //public string code { get; set; } = string.Empty;
    }
}
