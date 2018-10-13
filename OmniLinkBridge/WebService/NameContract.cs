using System.Runtime.Serialization;

namespace OmniLinkBridge.WebAPI
{
    [DataContract]
    public class NameContract
    {
        [DataMember]
        public ushort id { get; set; }

        [DataMember]
        public string name { get; set; }
    }
}
