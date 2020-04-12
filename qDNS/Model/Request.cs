using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace qDNS.Model
{
	public class Request : IDeepCloneable
	{
		public RequestHeader Header { get; set; } = new RequestHeader();
		public List<RequestQuestion> Questions { get; set; } = new List<RequestQuestion>();
		public List<ResponseRecord> Answers { get; set; } = new List<ResponseRecord>();
		public List<ResponseRecord> AuthorityRR { get; set; } = new List<ResponseRecord>();
		public List<ResponseRecord> AdditionalRR { get; set; } = new List<ResponseRecord>();

		public Request()
		{
			
		}

		public override string ToString() => $"{Header} {string.Join(", ", Questions.Select(x => x.ToString()))}";

		public Request(string host)
		{
			Header.Identifiation = 1;
			Header.Flags |= HeaderFlags.ReqursionDesired;
			Questions.Add(new RequestQuestion
			{
				Name = host,
				Type = RecordType.A,
				Class = RecordClass.IN,
			});
		}

		public static Request Parse(byte[] data)
		{
			var template = new Request();
			Parse(data, template);
			return template;
		}

		protected static void Parse(byte[] data, Request template, bool log = false)
		{
			if (template == null)
			{
				template = new Request();
			}

			var r = template;

			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			if (log)
			{
				try
				{
					const int skip = 12;
					var hex = string.Join(" ", data.Skip(skip).Select(x => x.ToString("X2")));
					var ascii = string.Join(" ", Encoding.GetEncoding(1251).GetString(data).Skip(skip).Select(x => " " + x));
					Console.WriteLine(hex);
					Console.WriteLine(ascii);
				}
				catch { }
			}

			var b = 0;

			r.Header.Identifiation = UInt16(data, ref b);
			// r.Header.Flags = (HeaderFlags)(short)(data[b++]+(data[b++]<<8));
			r.Header.Flags = (HeaderFlags)UInt16(data, ref b);

			var questionsCount = UInt16(data, ref b);
			var answersCount = UInt16(data, ref b);
			var authorityRR = UInt16(data, ref b);
			var additionalRR = UInt16(data, ref b);

			for (int i = 0; i < questionsCount; i++)
			{
				var host = ParseHost(data, ref b);
				var type = (RecordType)UInt16(data, ref b);
				var @class = (RecordClass)UInt16(data, ref b);

				var q = new RequestQuestion
				{
					Name = host,
					Type = type,
					Class = @class,
				};

				r.Questions.Add(q);
			}

			ResponseRecord ReadResponseRecord()
			{
				string host;
				var c = data[b];
				if ((c & 0xC0) == 0xC0) // host compression enabled (11xx xxx)
				{
					var pointer = ((c & 0x3F) << 8) | data[b + 1];
					b += 2;
					host = ParseHost(data, ref pointer); // pointer will be promoted and let it be
				}
				else
				{
					host = ParseHost(data, ref b); // b will be promoted here
				}

				var type = (RecordType)UInt16(data, ref b);

				var rr = new ResponseRecord
				{
					Name = host,
					Type = type,
					Class = (RecordClass)UInt16(data, ref b),
					Ttl = (int)UInt32(data, ref b),
				};

				var rLen = UInt16(data, ref b);
				var rData = new byte[rLen];
				Array.Copy(data, b, rData, 0, rLen);
				b += rLen;
				rr.Data = rData;
				return rr;
			}
			for (var i = 0; i < answersCount; i++)
			{
				r.Answers.Add(ReadResponseRecord());
			}
			for (var i = 0; i < authorityRR; i++)
			{
				r.AuthorityRR.Add(ReadResponseRecord());
			}
			for (var i = 0; i < additionalRR; i++)
			{
				r.AdditionalRR.Add(ReadResponseRecord());
			}
		}

		static string ParseHost(byte[] data, ref int b)
		{
			var host = "";
			do
			{
				var c = data[b++];
				if (c == 0)
				{
					break;
				}

				var part = _enc.GetString(data, b, c);
				b += c;
				host += (host.Length == 0 ? "" : ".") + part;
			} while (true);
			return host;
		}

		public static byte[] SerializeHost(string host)
		{
			var buf = new byte[256];
			var r = 0;
			SerializeHost(buf, ref r, new Dictionary<string, ushort>(), host);
			var act = new byte[r];
			Array.Copy(buf, 0, act, 0, r);
			return act;
		}

		static void SerializeHost(byte[] data, ref int b, Dictionary<string, ushort> lookup, string host)
		{
			if (lookup.TryGetValue(host, out var ptr))
			{
				// write compression pointer
				data[b++] = (byte)((ptr >> 8) | 0xc0); // 11HH HHHH
				data[b++] = unchecked((byte)ptr);      // LLLL LLLL
			}
			else
			{
				lookup[host] = (ushort)b;
				var parts = host.Split('.');
				foreach (var part in parts)
				{
					data[b++] = checked((byte)part.Length);
					_enc.GetBytes(part.ToArray(), 0, part.Length, data, b);
					b += part.Length;
				}

				data[b++] = 0;
			}
		}

		private static Encoding _enc = Encoding.ASCII;//Encoding.GetEncoding(1251);

		static ushort UInt16(byte[] data, ref int b)
		{
			return (ushort)((data[b++] << 8) | data[b++]);
		}
		static void UInt16(byte[] data, ref int b, ushort val)
		{
			data[b++] = (byte)(val >> 8);
			data[b++] = (byte)(val);
		}

		static void UInt32(byte[] data, ref int b, uint val)
		{
			data[b++] = (byte)(val >> 24);
			data[b++] = (byte)(val >> 16);
			data[b++] = (byte)(val >> 8);
			data[b++] = (byte)(val);
		}

		static uint UInt32(byte[] data, ref int b)
		{
			return (uint)(0
				| (data[b++] << 24)
				| (data[b++] << 16)
				| (data[b++] << 8)
				| (data[b++])
			);
		}

		public byte[] Serialzie()
		{
			var data = new byte[5 * 1024];

			var b = 0;

			UInt16(data, ref b, Header.Identifiation);
			UInt16(data, ref b, (ushort)Header.Flags);
			UInt16(data, ref b, (ushort)Questions.Count);
			UInt16(data, ref b, (ushort)Answers.Count);
			UInt16(data, ref b, (ushort)AuthorityRR.Count);
			UInt16(data, ref b, (ushort)AdditionalRR.Count);

			var lookup = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < Questions.Count; i++)
			{
				var question = Questions[i];
				SerializeHost(data, ref b, lookup, question.Name);
				UInt16(data, ref b, (ushort)question.Type);
				UInt16(data, ref b, (ushort)question.Class);
			}

			foreach (var rrList in new[] { Answers, AuthorityRR, AdditionalRR })
			{
				for (int i = 0; i < rrList.Count; i++)
				{
					var answer = rrList[i];
					SerializeHost(data, ref b, lookup, answer.Name);
					UInt16(data, ref b, (ushort)answer.Type);
					UInt16(data, ref b, (ushort)answer.Class);
					UInt32(data, ref b, (uint)answer.Ttl);
					UInt16(data, ref b, checked((ushort)answer.Data.Length));
					Array.Copy(answer.Data, 0, data, b, answer.Data.Length);
					b += answer.Data.Length;
				}
			}

			var res = new byte[b];
			Array.Copy(data, res, b);
			return res;
		}

		public Request Clone()
		{
			return (Request)DeepClone();
		}

		public virtual object DeepClone()
		{
			var clone = (Request)MemberwiseClone();
			clone.Header = (RequestHeader)clone.Header.DeepClone();
			clone.Questions = new List<RequestQuestion>(clone.Questions.Select(x => (RequestQuestion)x.DeepClone()));
			clone.Answers = new List<ResponseRecord>(clone.Answers.Select(x => (ResponseRecord)x.DeepClone()));
			clone.AuthorityRR = new List<ResponseRecord>(clone.AuthorityRR.Select(x => (ResponseRecord)x.DeepClone()));
			clone.AdditionalRR = new List<ResponseRecord>(clone.AdditionalRR.Select(x=>(ResponseRecord)x.DeepClone()));
			return clone;
		}
	}

	public class Response : Request
	{
		public Response()
		{

		}

		public Response(ResponseRecord response)
		{
			Questions.Add(new RequestQuestion
			{
				Name = response.Name,
				Type = response.Type,
				Class = response.Class,
			});
			Header.Flags = HeaderFlags.IsResponse;
			Answers.Add(response);
		}

		public static implicit operator Response(ResponseRecord rr)
		{
			return new Response(rr);
		}

		public new Response Clone()
		{
			return (Response)DeepClone();
		}

		public override string ToString()
		{
			return $"{base.ToString()} {string.Join(", ", Answers.Select(x => x.ToString()))}";
		}

		public static Response Parse(byte[] data)
		{
			var template = new Response();
			Parse(data, template);
			return template;
		}

		// public override obj
	}
}
