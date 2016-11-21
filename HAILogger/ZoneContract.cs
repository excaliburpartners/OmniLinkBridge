using HAI_Shared;
using System.Runtime.Serialization;

namespace HAILogger
{
    [DataContract]
    public class ZoneContract
    {
        [DataMember]
        public ushort id { get; set; }

        [DataMember]
        public enuZoneType zonetype { get; set; }

        [DataMember]
        public string name { get; set; }

        [DataMember]
        public string status { get; set; }

        [DataMember]
        public string temp { get; set; }
    }
}
