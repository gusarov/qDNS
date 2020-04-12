using System;

namespace qDNS.Model
{
	[Flags]
	public enum HeaderFlags : ushort
	{
		/* QR */ IsResponse = 1 << 15, // 1xxx xxxx xxxx xxxx
		/* OP */ // Query = 1 << 15, // x111 1xxx xxxx xxxx
		/* AA */ AuthoritativeAnswer = 1 << 10, // xxxx x1xx xxx xxx
		/* TC */ Truncation = 1 << 9, // xxxx xx1x xxxx xxxx
		/* RD */ ReqursionDesired = 1 << 8, // xxxx xxx1 xxxx xxxx
		/* RA */ ReqursionAvailable = 1 << 7, // xxxx xxxx 1xxx xxxx
		/* Z  */ // xxxx xxxx x000 xxxx
		/* RC */ // xxxx xxxx xxxx 1111

		// RCODES:
		RCode_NoSuchName = 3,
	}

	public enum ResponseCode : byte
	{
		NoError = 0,
		FormErr = 1,
		ServFail = 2,
		NoSuchName = 3,
		NotImp = 4,
		Refused = 5,
		YXDomain = 6,
		YXRRSet = 7,
		NXRRSet = 8,
		NotAuth = 9,
		NotZone = 10,
		DSOTYPENI = 11,

		// Extended RCodes:
		BADVERS = 16,
		BADSIG = 16,
		BADKEY = 17,
		BADTIME = 18,
		BADMODE = 19,
		BADNAME = 20,
		BADALG = 21,
		BADTRUNC = 22,
		BADCOOKIE = 23,
	}

	public enum OperationCode : byte
	{
		Query,
		IQuery,
		Status,
	}

	public static class HeaderFlagsExtensions
	{
		public static HeaderFlags GetPureFlags(this HeaderFlags flags)
		{
			return (HeaderFlags)((ushort)flags & 0b_1000_0111_1111_0000);
		}
		public static ResponseCode GetResponseCode(this HeaderFlags flags)
		{
			return (ResponseCode)((uint)flags & 15);
		}
		public static HeaderFlags SetOperationCode(this HeaderFlags flags, ResponseCode code)
		{
			return (HeaderFlags)(((ushort)flags & ~15) | (ushort)code);
		}
		public static OperationCode GetOperationCode(this HeaderFlags flags)
		{
			return (OperationCode)(((uint)flags >> 11) & 15);
		}
	}
}