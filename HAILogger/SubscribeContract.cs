using System.Runtime.Serialization;

namespace HAILogger
{
    [DataContract]
    public class SubscribeContract
    {
        [DataMember]
        public string callback { get; set; }
    }
}
