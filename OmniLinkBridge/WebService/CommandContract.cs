using System.Runtime.Serialization;

namespace OmniLinkBridge.WebAPI
{
    [DataContract]
    public class CommandContract
    {
        [DataMember]
        public ushort id { get; set; }

        [DataMember]
        public ushort value { get; set; }
    }
}
