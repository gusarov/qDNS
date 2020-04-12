using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qDNS.Model;
using System.Net;
using System.Linq;
using System.Net.Sockets;

namespace qDNS.Tests
{
	[TestClass]
	public class DnsIntegrationTest
	{
		private readonly IPEndPoint _testEndpoint = new IPEndPoint(new IPAddress(new byte[] {127, 0, 0, 2}), 53);

		[TestMethod]
		public void Should_10_respond_on_request()
		{
			using (var srv = new DnsServer())
			{
				srv.Run(_testEndpoint);

				var cli = new UdpClient();
				cli.Connect(_testEndpoint);
				var req = new Request("dnstest.xkip.me");

				var buf = req.Serialzie();
				cli.Send(buf, buf.Length);
				IPEndPoint ep = null;
				var data = cli.Receive(ref ep);
				var res = Response.Parse(data);

				Assert.AreEqual(_testEndpoint, ep);
				Assert.AreEqual(1, res.Header.Identifiation);
				Assert.IsTrue(res.Header.Flags.HasFlag(HeaderFlags.IsResponse));
				Assert.AreEqual(1, res.Questions.Count);
				Assert.AreEqual("dnstest.xkip.me", res.Questions[0].Name);
				Assert.AreEqual(1, res.Questions[0].Type);
				Assert.AreEqual(1, res.Questions[0].Class);

				Assert.AreEqual(2, res.Answers.Count);
				var answers = res.Answers.OrderBy(x => x.Data[0]).ToArray();

				Assert.AreEqual("dnstest.xkip.me", answers[0].Name);
				Console.WriteLine(string.Join(".", answers[0].Data.Select(x => x.ToString())));
				CollectionAssert.AreEqual(new byte[] {1, 2, 3, 4}, answers[0].Data);
				Assert.AreEqual(1, answers[0].Type);
				// Assert.AreEqual(120U, answers[0].Ttl);
				Assert.AreEqual(1, answers[0].Class);

				Assert.AreEqual("dnstest.xkip.me", answers[1].Name);
				Console.WriteLine(string.Join(".", answers[1].Data.Select(x => x.ToString())));
				CollectionAssert.AreEqual(new byte[] {5, 6, 7, 8}, answers[1].Data);
				Assert.AreEqual(1, answers[1].Type);
				// Assert.AreEqual(120U, answers[1].Ttl);
				Assert.AreEqual(1, answers[1].Class);
			}
		}
	}
}
