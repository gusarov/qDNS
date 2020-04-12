using System.Text;

namespace qDNS.Model
{
    public class ResponseRecord
    {
        public string Name;
        public ushort Type;
        public ushort Class;
        public uint Ttl;
        public byte[] Data;
    }
}
