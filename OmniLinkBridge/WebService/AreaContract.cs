using System.Runtime.Serialization;

namespace OmniLinkBridge.WebAPI
{
    [DataContract]
    public class AreaContract
    {
        [DataMember]
        public ushort id { get; set; }

        [DataMember]
        public string name { get; set; }

        [DataMember]
        public string burglary { get; set; }

        [DataMember]
        public string co { get; set; }

        [DataMember]
        public string fire { get; set; }

        [DataMember]
        public string water { get; set; }

        [DataMember]
        public string mode { get; set; }
    }
}
