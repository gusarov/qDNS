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
		private static readonly IPEndPoint _testEndpoint = new IPEndPoint(new IPAddress(new byte[] {127, 0, 0, 2}), 53);

		static DnsServer _srv;

		[ClassInitialize]
		public static void Init(TestContext ctx)
		{
			_srv = new DnsServer();
			_srv.ClearForwarding();
			_srv.AddForwarding(new IPAddress(new byte[] { 8, 8, 8, 8 }));
			_srv.Run(_testEndpoint);
		}

		[ClassCleanup]
		public static void Clean()
		{
			_srv.Dispose();
		}

		[TestMethod]
		public void Should_10_respond_on_request()
		{
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
			Assert.AreEqual(RecordType.A, res.Questions[0].Type);
			Assert.AreEqual(RecordClass.IN, res.Questions[0].Class);

			Assert.AreEqual(2, res.Answers.Count);
			var answers = res.Answers.OrderBy(x => x.Data[0]).ToArray();

			Assert.AreEqual("dnstest.xkip.me", answers[0].Name);
			Console.WriteLine(string.Join(".", answers[0].Data.Select(x => x.ToString())));
			CollectionAssert.AreEqual(new byte[] {1, 2, 3, 4}, answers[0].Data);
			Assert.AreEqual(RecordType.A, answers[0].Type);
			// Assert.AreEqual(120U, answers[0].Ttl);
			Assert.AreEqual(RecordClass.IN, answers[0].Class);

			Assert.AreEqual("dnstest.xkip.me", answers[1].Name);
			Console.WriteLine(string.Join(".", answers[1].Data.Select(x => x.ToString())));
			CollectionAssert.AreEqual(new byte[] {5, 6, 7, 8}, answers[1].Data);
			Assert.AreEqual(RecordType.A, answers[1].Type);
			// Assert.AreEqual(120U, answers[1].Ttl);
			Assert.AreEqual(RecordClass.IN, answers[1].Class);
		}

		[TestMethod]
		[Timeout(10000)]
		public void Should_20_parse_big_fragmented_request()
		{
			var cli = new UdpClient();
			cli.Connect(_testEndpoint);
			var name = "test.";
			for (int i = 0; i < 9; i++)
			{
				name += name;
			}
			name = name.TrimEnd('.');

			Console.WriteLine(name.Length);
			var req = new Request(name);
			var buf = req.Serialzie();
			cli.Send(buf, buf.Length);
			IPEndPoint ep = null;
			var data = cli.Receive(ref ep);
			var res = Response.Parse(data);

			// the only thing I care here is 
		}
	}
}
