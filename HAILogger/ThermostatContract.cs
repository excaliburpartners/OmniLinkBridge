using HAI_Shared;
using System.Runtime.Serialization;

namespace HAILogger
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
    }
}
