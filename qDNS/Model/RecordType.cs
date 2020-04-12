using System;

namespace qDNS.Model
{
	public enum RecordClass : ushort
	{
		/// <summary>
		/// Internet
		/// </summary>
		IN = 1,

		/// <summary>
		/// CSNET
		/// </summary>
		[Obsolete("", true)]
		CS = 2,

		/// <summary>
		/// CHAOS
		/// </summary>
		CH = 3,

		/// <summary>
		/// Hesiod
		/// </summary>
		HS = 4,

		/// <summary>
		/// (*) Any class
		/// </summary>
		Any = 255
	}

	public enum RecordType : ushort
	{
		/// <summary>
		/// IPv4 host
		/// </summary>
		A = 1,

		/// <summary>
		/// Authoritative Name Server
		/// </summary>
		NS = 2,

		[Obsolete("Use MX", true)]
		MD = 3,

		[Obsolete("Use MX", true)]
		MF = 4,

		/// <summary>
		/// Canonical name for an alias
		/// </summary>
		CNAME = 5,

		/// <summary>
		/// Start of a zone of authority
		/// </summary>
		SOA = 6,

		[Obsolete("Experimental")]
		MB = 7,

		[Obsolete("Experimental")]
		MG = 8,

		[Obsolete("Experimental")]
		MR = 9,

		[Obsolete("Experimental")]
		NULL = 10,

		/// <summary>
		/// Well known service description
		/// </summary>
		[Obsolete("")]
		WKS = 11,

		/// <summary>
		/// Domain name pointer
		/// </summary>
		PTR = 12,

		/// <summary>
		/// Host information
		/// </summary>
		HINFO = 13,

		/// <summary>
		/// Mailbox or mail list information
		/// </summary>
		MINFO = 14,

		/// <summary>
		/// Mail exchange
		/// </summary>
		MX = 15,

		/// <summary>
		/// Text strings
		/// </summary>
		TXT = 16,

		/// <summary>
		/// IPv6 address record
		/// </summary>
		AAAA = 28,

		/// <summary>
		/// Service locator
		/// </summary>
		SRV = 33,

		/// <summary>
		/// DHCP identifier
		/// </summary>
		DHCID = 49,

		/// <summary>
		/// A request for a transfer of an entire zone
		/// </summary>
		AXFR = 252,

		/// <summary>
		/// A request for mailbox-related records (MB, MG or MR)
		/// </summary>
		MAILB = 253,

		/// <summary>
		/// A request for mail agent RRs (Obsolete - see MX)
		/// </summary>
		[Obsolete("Use MX", true)]
		MAILA = 254,

		/// <summary>
		/// (*) A request for all records
		/// </summary>
		All = 255,
	}
}