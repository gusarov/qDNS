namespace qDNS.Model
{
	public class RequestHeader : IDeepCloneable
	{
		public ushort Identifiation; // 16
		public HeaderFlags Flags; // 16

		public override string ToString()
		{
			return $"#{Identifiation} {Flags.GetPureFlags()} {Flags.GetOperationCode()} {Flags.GetResponseCode()}";
		}

		public object DeepClone()
		{
			var clone = (RequestHeader) MemberwiseClone();

			return clone;
		}
	}
}