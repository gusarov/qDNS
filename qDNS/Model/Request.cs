using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qDNS.Model
{
    using RequestQuestionsSection = List<RequestQuestion>;
    using RequestAnswersSection = List<ResponseRecord>;

    public class Request
    {
        public static Request Parse(byte[] data, bool log = false)
        {
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

            var r = new Request();

            var b = 0;

            r.Header.Identifiation = UInt16(data, ref b);
            // r.Header.Flags = (HeaderFlags)(short)(data[b++]+(data[b++]<<8));
            r.Header.Flags = (HeaderFlags)UInt16(data, ref b);
            var questionsCount = UInt16(data, ref b);
            var answersCount = UInt16(data, ref b);
            var AuthorityRR = UInt16(data, ref b);
            var AdditionalRR = UInt16(data, ref b);

            for (int i = 0; i < questionsCount; i++)
            {
                var host = ParseHost(data, ref b);

                var q = new RequestQuestion
                {
                    Name = host,
                    Type = UInt16(data, ref b),
                    Class = UInt16(data, ref b),
                };

                r.Questions.Add(q);
            }
            for (int i = 0; i < answersCount; i++)
            {
                string host;
                var c = data[b];
                if ((c & 0xC0) == 0xC0) // host compression enabled
                {
                    var pointer = ((c & 0x3F ) << 8) | data[b+1];
                    b += 2;
                    host = ParseHost(data, ref pointer);
                }
                else
                {
                    host = ParseHost(data, ref b);
                }

                var type = UInt16(data, ref b);

                var rr = new ResponseRecord
                {
                    Name = host,
                    Type = type,
                    Class = UInt16(data, ref b),
                    Ttl = UInt32(data, ref b),
                };

                var rlen = UInt16(data, ref b);
                var rdata = new byte[rlen];
                Array.Copy(data, b, rdata, 0, rlen);
                b += rlen;
                rr.Data = rdata;

                r.Answers.Add(rr);
            }
            return r;
        }

        static string ParseHost(byte[] data, ref int b)
        {
            string host = "";
            do
            {
                var c = data[b++];
                if (c == 0)
                {
                    break;
                }

                var part = _enc.GetString(data, b, c);
                b += c;
                host += "." + part;
            } while (true);

            return host.TrimStart('.');
        }

        static void SerializeHost(byte[] data, ref int b, Dictionary<string, ushort> lookup, string host)
        {
            if (lookup.TryGetValue(host, out var ptr))
            {
                // write compression pointer
                data[b++] = (byte) ((ptr >> 8) | 0xc0); // 11HH HHHH
                data[b++] = unchecked((byte) ptr);      // LLLL LLLL
            }
            else
            {
                lookup[host] = (ushort)b;
                var parts = host.Split('.');
                foreach (var part in parts)
                {
                    data[b++] = checked((byte) part.Length);
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
            return (uint)(
                  (data[b++] << 24)
                | (data[b++] << 16)
                | (data[b++] << 8)
                | data[b++]
                );
        }

        public byte[] Serialzie()
        {
            var data = new byte[256];


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
                UInt16(data, ref b, question.Type);
                UInt16(data, ref b, question.Class);
            }

            foreach (var rrList in new[] {Answers, AuthorityRR, AdditionalRR})
            {
                for (int i = 0; i < rrList.Count; i++)
                {
                    var answer = Answers[i];
                    SerializeHost(data, ref b, lookup, answer.Name);
                    UInt16(data, ref b, answer.Type);
                    UInt16(data, ref b, answer.Class);
                    UInt32(data, ref b, answer.Ttl);
                    UInt16(data, ref b, checked((ushort) answer.Data.Length));
                    Array.Copy(answer.Data, 0, data, b, answer.Data.Length);
                    b += answer.Data.Length;
                }
            }

            var res = new byte[b];
            Array.Copy(data, res, b);
            return res;
        }

        public RequestHeader Header { get; set; } = new RequestHeader();
        public RequestQuestionsSection Questions { get; set; } = new RequestQuestionsSection();
        public RequestAnswersSection Answers { get; set; } = new RequestAnswersSection();
        public RequestAnswersSection AuthorityRR { get; set; } = new RequestAnswersSection();
        public RequestAnswersSection AdditionalRR { get; set; } = new RequestAnswersSection();
    }

    class Response : Request
    {
    }
}
