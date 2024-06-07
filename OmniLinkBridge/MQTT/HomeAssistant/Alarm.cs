using Newtonsoft.Json;
using System.Collections.Generic;

namespace OmniLinkBridge.MQTT.HomeAssistant
{
    public class Alarm : Device
    {
        public Alarm(DeviceRegistry deviceRegistry) : base(deviceRegistry)
        {

        }

        public string command_topic { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string command_template { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string code { get; set; }

        public bool code_arm_required { get; set; } = false;

        public bool code_disarm_required { get; set; } = false;

        public bool code_trigger_required { get; set; } = false;

        public List<string> supported_features { get; set; } = new List<string>(new string[] { 
            "arm_home", "arm_away", "arm_night", "arm_vacation" });
    }
}
