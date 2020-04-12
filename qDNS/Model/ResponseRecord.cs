using System;
using System.Linq;
using System.Text;

namespace qDNS.Model
{
	public class ResponseRecord : IDeepCloneable
	{
		public object DeepClone()
		{
			var clone = (ResponseRecord) MemberwiseClone();
			clone.Data = new byte[Data.Length];
			Array.Copy(Data, clone.Data, Data.Length);
			return clone;
		}

		public override string ToString()
		{
			return $"{Name} = {(Data.Length == 4 ? string.Join(".",Data.Select(x=>x.ToString())): string.Join("", Data.Select(x => x.ToString("X2"))))}";
		}

		public string Name;
		public ushort Type;
		public ushort Class;
		public uint Ttl;
		public byte[] Data;
	}
}