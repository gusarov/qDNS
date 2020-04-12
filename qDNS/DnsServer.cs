using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using qDNS.Model;
using System.Threading;

namespace qDNS
{
	public class DnsServer : IDisposable
	{
		static void Main()
		{
			new DnsServer().Run();
			Console.ReadLine();
		}

		public void Run(IPEndPoint endPoint = null)
		{
			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (var networkInterface in interfaces)
			{

				var adapterProperties = networkInterface.GetIPProperties();
				var dnsServers = adapterProperties.DnsAddresses;
				if (dnsServers.Count > 0)
				{
					Console.WriteLine(networkInterface.Description);
					foreach (var dns in dnsServers)
					{
						Console.WriteLine($"  DNS Servers: {dns}");
						_address.Add(dns);
					}

					Console.WriteLine();
				}
			}

			if (endPoint != null)
			{
				var udp0 = new UdpClient(endPoint);
				udp0.BeginReceive(UdpReceived, udp0);
				_clients.Add(udp0);
			}
			else
			{
				var udp1 = new UdpClient(53, AddressFamily.InterNetwork);
				udp1.BeginReceive(UdpReceived, udp1);
				_clients.Add(udp1);

				var udp2 = new UdpClient(53, AddressFamily.InterNetworkV6);
				udp2.BeginReceive(UdpReceived, udp2);
				_clients.Add(udp2);
			}
		}

		private readonly List<UdpClient> _clients = new List<UdpClient>();

		private readonly List<IPAddress> _address = new List<IPAddress>();

		private void UdpReceived(IAsyncResult ar)
		{
			var udp = (UdpClient) ar.AsyncState;
			udp.BeginReceive(UdpReceived, ar.AsyncState); // infinite async

			try
			{
				IPEndPoint ep = null;
				var data = udp.EndReceive(ar, ref ep);
				Console.WriteLine($"Received Request: {data.Length} bytes from {ep}:");

				Console.WriteLine(string.Join("", data.Select(x => x.ToString("X2"))));

				var resp = Handle(data);
				Console.WriteLine("Returning Response...");
				udp.Send(resp, resp.Length, ep);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		private Cache _cache = new Cache();

		byte[] Handle(byte[] data)
		{
			// There is 3 ways
			// 1 - it is served from cache
			// 2 - it is delegated/forwarded to another server
			// 3 - it is served from cache but since TTL outdated a new background update request issued

			var req = Request.Parse(data);
			Console.WriteLine(req.ToString());
			try
			{
				// usually it have only one dns host request
				var name = Cache.GetCacheableQuery(req);
				if (name.NotDefault)
				{
					var cachedResult = _cache.Get(name);
					if (cachedResult != null)
					{
						var response = cachedResult.Response.Clone();

						const int minTtl = 3;

						if (cachedResult.IsOutdated)
						{
							Task.Run(delegate
							{
								using (ConsoleUtil.Color(ConsoleColor.Yellow))
								{
									Console.WriteLine("Requesting updated record...");
								}
								Forward(data);
							}); // Forwarder must update the cache, but meanwhile we will respond immediately with outdated answer

							// also reset ttl
							response.Answers.ForEach(x => x.Ttl = minTtl);
							response.AuthorityRR.ForEach(x => x.Ttl = minTtl);
							response.AdditionalRR.ForEach(x => x.Ttl = minTtl);
						}

						// patch TTL with the time remaining since cached?
						var secondsAgo = (int)(DateTime.UtcNow - cachedResult.CachedAtUtc).TotalSeconds;
						response.Answers.ForEach(x => x.Ttl = (uint)Math.Min(minTtl, x.Ttl - secondsAgo));
						response.AuthorityRR.ForEach(x => x.Ttl = (uint)Math.Min(minTtl, x.Ttl - secondsAgo));
						response.AdditionalRR.ForEach(x => x.Ttl = (uint)Math.Min(minTtl, x.Ttl - secondsAgo));

						response.Questions = req.Questions;
						response.Header.Identifiation = req.Header.Identifiation;

						using (ConsoleUtil.Color(ConsoleColor.Green))
						{
							Console.WriteLine("Hit Cache!");
							Console.WriteLine(response);
						}

						return response.Serialzie();
					}
				}

				/*
				if (req.Questions[0].Name.ToLowerInvariant() == "xxx")
				{
					Console.WriteLine("OVERRIDING RESPONSE");
					req.Answers.Add(new ResponseRecord
					{
						Data = new byte[] {10, 0, 0, 22},
						Class = 1,
						Name = req.Questions[0].Name,
						Ttl = 3 * 60,
						Type = 1,
					});
					req.Header.Flags = (HeaderFlags) 0x8180; // have no idea about details, just a usual response
					Console.WriteLine(JsonConvert.SerializeObject(req, Formatting.Indented));

					return req.Serialzie();
				}
				*/

			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			Console.WriteLine();
			// forward synchronously

			using (ConsoleUtil.Color(ConsoleColor.Red))
			{
				Console.WriteLine("Cache miss, direct request...");
			}

			return Forward(data);
		}

		public byte[] Forward(byte[] data)
		{
			// todo ask all at the same time to reduce delay
			var epi = 0;
			retry:
			IPAddress ep;
			lock (_address)
			{
				ep = _address[epi];
			}

			using (var udp = new UdpClient())
			{
				Console.WriteLine($">>> Forward to {ep}");
				var ei = new IPEndPoint(ep, 53);
				udp.Connect(ei);
				udp.Send(data, data.Length);

				var rTask = udp.ReceiveAsync();
				if (!rTask.Wait(1500))
				{
					lock (_address)
					{
						var old = _address[epi];
						epi++;
						if (epi > _address.Count)
						{
							throw new Exception("Not found");
						}

						Console.WriteLine($"DNS Server {old} is not responding, switching to {_address[epi]}");
					}

					goto retry;
				}

				// done, make sure it is prioritized
				if (epi != 0)
				{
					lock (_address)
					{
						_address.RemoveAt(epi);
						_address.Insert(0, ep);
						Console.WriteLine($"Moved on top: {ep}");
					}
				}

				var resp = rTask.Result.Buffer;
				Console.WriteLine($"<<< received from {rTask.Result.RemoteEndPoint}");

				var res = Response.Parse(resp);

				#region print

				using (ConsoleUtil.Color(ConsoleColor.DarkMagenta))
				{
					try
					{
						Console.WriteLine(string.Join("", resp.Select(x => x.ToString("X2"))));
						Console.WriteLine(JsonConvert.SerializeObject(res, Formatting.Indented));
						Console.WriteLine(res);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
					}
				}

				#endregion

				_cache.Set(res, rTask.Result.RemoteEndPoint);

				return resp;
			}
		}

		public void Dispose()
		{
			foreach (var udpClient in _clients)
			{
				try
				{
					udpClient.Close();
				}
				catch
				{
				}
			}
		}

	}

	public class ConsoleUtil
	{
		public static IDisposable Color(ConsoleColor color)
		{
			var orig = Console.ForegroundColor;
			var obj = new object();
			Monitor.Enter(obj);
			Console.ForegroundColor = color;
			return new Scope(delegate
			{
				Console.ForegroundColor = orig;
				Monitor.Exit(obj);
			});
		}
	}

	public class Scope : IDisposable
	{
		private readonly Action _act;

		public Scope(Action act)
		{
			_act = act;
		}

		public void Dispose()
		{
			_act();
		}
	}
}
