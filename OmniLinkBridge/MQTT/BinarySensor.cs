using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.MQTT
{
    public class BinarySensor : Device
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum DeviceClass
        {
            battery,
            door,
            garage_door,
            gas,
            moisture,
            motion,
            problem,
            smoke,
            window
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceClass? device_class { get; set; }
    }
}
