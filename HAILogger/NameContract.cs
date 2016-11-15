using System.Runtime.Serialization;

namespace HAILogger
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
