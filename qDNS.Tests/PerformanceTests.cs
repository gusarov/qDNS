using Microsoft.VisualStudio.TestTools.UnitTesting;
using qDNS.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace qDNS.Tests
{
	[TestClass]
	public class PerformanceTests : BaseIntegrationTests
	{
		[TestMethod]
		public void Should_respond_very_quickly()
		{
			try
			{
				Console.Enable = false;

				using (var cli = new UdpClient())
				{

					cli.Connect(_testEndpoint);

					var req = new Request("dnstest.xkip.me");
					var buf = req.Serialize();

					// warm up
					cli.Send(buf, buf.Length);
					IPEndPoint ep = null;
					cli.Receive(ref ep);

					var started = Stopwatch.StartNew();
					var cnt = 0;

					while (started.ElapsedMilliseconds < 1000)
					{
						const int batch = 100;
						for (int i = 0; i < batch; i++)
						{
							buf[3] = (byte)cnt; // id

							cli.Send(buf, buf.Length);
							var data = cli.Receive(ref ep);
							// var res = Response.Parse(data);
							// CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, res.Answers.Where(x => x.Data[0] == 1).First().Data);
						}
						cnt += batch;
					}

					started.Stop();
					var perf = cnt / started.Elapsed.TotalSeconds;
					System.Console.WriteLine("{0:0.0}", perf);
					Assert.IsTrue(perf > 8000, $"{cnt} requests per second");
				}
			}
			finally
			{
				Console.Enable = true;
			}
		}
	}
}
