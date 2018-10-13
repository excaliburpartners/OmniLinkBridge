using System.Runtime.Serialization;

namespace OmniLinkBridge.WebAPI
{
    [DataContract]
    public class SubscribeContract
    {
        [DataMember]
        public string callback { get; set; }
    }
}
