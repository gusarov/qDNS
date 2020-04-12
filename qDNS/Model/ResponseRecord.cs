using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace qDNS.Model
{
	public class ResponseRecord : IDeepCloneable
	{
		public ResponseRecord()
		{

		}

		public ResponseRecord(RecordType type, string name, byte[] data, int ttl)
		{
			Type = type;
			Name = name;
			Data = data;
			Ttl = ttl;
		}

		public ResponseRecord(string name, IPAddress address, int ttl)
		{
			Name = name;
			Ttl = ttl;
			Class = RecordClass.IN;
			switch (address.AddressFamily)
			{
				case AddressFamily.InterNetwork:
					Type = RecordType.A;
					break;
				case AddressFamily.InterNetworkV6:
					Type = RecordType.AAAA;
					break;
				default:
					throw new Exception("AddressFamily is not supported");
			}
			Data = address.GetAddressBytes();
		}

		public object DeepClone()
		{
			var clone = (ResponseRecord) MemberwiseClone();
			clone.Data = new byte[Data.Length];
			Array.Copy(Data, clone.Data, Data.Length);
			return clone;
		}

		string RData
		{
			get
			{
				switch (Type)
				{
					case RecordType.A: // IPv4
						return string.Join(".", Data.Select(x => x.ToString()));
					case RecordType.AAAA: // IPv6
						return new IPAddress(Data).ToString();
					default:
						return string.Join("", Data.Select(x => x.ToString("X2")));
				}
			}
		}

		public override string ToString()
		{

			return $"{Type} {Name} = {RData}";
		}

		public string Name;
		public RecordType Type;
		public RecordClass Class = RecordClass.IN;
		public int Ttl;
		public byte[] Data;
	}

}