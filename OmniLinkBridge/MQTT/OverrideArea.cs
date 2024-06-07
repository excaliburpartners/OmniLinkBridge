using System.Collections.Generic;

namespace OmniLinkBridge.MQTT
{
    public class OverrideArea
    {
        public bool code_arm { get; set; }

        public bool code_disarm { get; set; }

        public bool arm_home { get; set; } = true;

        public bool arm_away { get; set; } = true;

        public bool arm_night { get; set; } = true;

        public bool arm_vacation { get; set; } = true;
    }
}
