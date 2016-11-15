using System.Runtime.Serialization;

namespace HAILogger
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
