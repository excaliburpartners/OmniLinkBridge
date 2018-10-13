using HAI_Shared;
using System.Runtime.Serialization;

namespace OmniLinkBridge.WebAPI
{
    [DataContract]
    public class ThermostatContract
    {
        [DataMember]
        public ushort id { get; set; }

        [DataMember]
        public string name { get; set; }

        [DataMember]
        public ushort temp { get; set; }

        [DataMember]
        public ushort humidity { get; set; }

        [DataMember]
        public ushort coolsetpoint { get; set; }

        [DataMember]
        public ushort heatsetpoint { get; set; }

        [DataMember]
        public enuThermostatMode mode { get; set; }

        [DataMember]
        public enuThermostatFanMode fanmode { get; set; }

        [DataMember]
        public enuThermostatHoldMode hold { get; set; }

        [DataMember]
        public string status { get; set; }
    }
}
