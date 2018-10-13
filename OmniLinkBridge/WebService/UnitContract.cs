using System.Runtime.Serialization;

namespace OmniLinkBridge.WebAPI
{
    [DataContract]
    public class UnitContract
    {
        [DataMember]
        public ushort id { get; set; }

        [DataMember]
        public string name { get; set; }

        [DataMember]
        public ushort level { get; set; }
    }
}
