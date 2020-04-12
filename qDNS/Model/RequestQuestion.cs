using System;
using System.Collections.Generic;

namespace qDNS.Model
{
	public class RequestQuestion : IEquatable<RequestQuestion>, IDeepCloneable
	{
		public string Name;
		public ushort Type;
		public ushort Class;

		public override string ToString()
		{
			return $"{Name} ({Type}:{Class})";
		}

		public object DeepClone()
		{
			var clone = (RequestQuestion)MemberwiseClone();
			return clone;
		}

		public override bool Equals(object obj) => Equals(obj as RequestQuestion);

		public bool Equals(RequestQuestion other) =>
			other != null && Name == other.Name && Type == other.Type && Class == other.Class;

		public override int GetHashCode()
		{
			var hashCode = -984369542;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
			hashCode = hashCode * -1521134295 + Type.GetHashCode();
			hashCode = hashCode * -1521134295 + Class.GetHashCode();
			return hashCode;
		}


	}
}
