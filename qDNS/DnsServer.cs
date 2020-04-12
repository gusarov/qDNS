#define ASYNC

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using qDNS.Model;
using System.Text.RegularExpressions;

namespace qDNS
{
	public class DnsServer : IDisposable
	{
		static void Main(string[] args)
		{
			using (var srv = new DnsServer())
			{

				for (var i = 0; i < args.Length; i++)
				{
					if (IPAddress.TryParse(args[i], out var ip))
					{
						var name = args[++i];
						Response resp = new ResponseRecord(name, ip, 24 * 60 * 60); // limit this for client to reduce client leaks
						// resp.Header.Flags |= HeaderFlags.AuthoritativeAnswer;
						srv._cache.Set(resp, default);
					}
				}

				srv.Run();
				Console.ReadLine();
			}
		}

		public DnsServer()
		{
			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			var addresses = new HashSet<IPAddress>();
			foreach (var networkInterface in interfaces)
			{

				var adapterProperties = networkInterface.GetIPProperties();
				foreach (var item in adapterProperties.UnicastAddresses.Select(x => x.Address))
				{
					_myAddress.Add(item);
				}
				foreach (var item in adapterProperties.UnicastAddresses)
				{
					System.Console.WriteLine(item);
				}

				var dnsServers = adapterProperties.DnsAddresses;
				if (dnsServers.Count > 0)
				{
					Console.WriteLine(networkInterface.Description);
					foreach (var dns in dnsServers)
					{
						Console.WriteLine($"  DNS Servers: {dns}");
						addresses.Add(dns);
					}

					Console.WriteLine();
				}
			}
			addresses.Add(new IPAddress(new byte[] { 1, 1, 1, 1 })); // Cloudflare
			addresses.Add(new IPAddress(new byte[] { 8, 8, 8, 8 })); // Google

			_forwardTo.AddRange(addresses.OrderBy(x => x.AddressFamily));
		}

		public void ClearForwarding()
		{
			_forwardTo.Clear();
		}

		public void AddForwarding(IPAddress address)
		{
			_forwardTo.Add(address);
		}

		public void Run(IPEndPoint endPoint = null)
		{

			

			if (endPoint != null)
			{
				var udp0 = new UdpClient(endPoint);
				udp0.Client.DontFragment = true;
				udp0.BeginReceive(UdpReceived, udp0);
				_clients.Add(udp0);
			}
			else
			{
				var udp1 = new UdpClient(53);
				udp1.Client.DontFragment = true;
				// Console.WriteLine(udp1.ExclusiveAddressUse);
				// Console.WriteLine(udp1.Client.DualMode);
				// udp1.ExclusiveAddressUse
				// udp1.Client.
				udp1.BeginReceive(UdpReceived, udp1);
				_clients.Add(udp1);

				/*

				var udp2 = new UdpClient(53, AddressFamily.InterNetworkV6);
				udp2.BeginReceive(UdpReceived, udp2);
				_clients.Add(udp2);

				var udp3 = new UdpClient(5353, AddressFamily.InterNetworkV6);
				udp3.BeginReceive(UdpReceived, udp3);
				_clients.Add(udp3);
				*/
			}
		}

		private readonly List<UdpClient> _clients = new List<UdpClient>();

		private readonly List<IPAddress> _forwardTo = new List<IPAddress>();
		private readonly HashSet<IPAddress> _myAddress = new HashSet<IPAddress>
		{
			IPAddress.Loopback,
			IPAddress.IPv6Loopback,
		};

		private void UdpReceived(IAsyncResult ar)
		{
			try
			{
				var udp = (UdpClient)ar.AsyncState;
#if ASYNC
			udp.BeginReceive(UdpReceived, ar.AsyncState); // infinite async
#endif

				using (ConsoleUtil.UseColor(ConsoleColor.Gray))
				{
					try
					{
						IPEndPoint ep = null;
						var data = udp.EndReceive(ar, ref ep);
						Console.WriteLine($"Received Request: {data.Length} bytes from {ep}:");
						Console.WriteLine(string.Join("", data.Select(x => x.ToString("X2"))));

						var resp = Handle(data, ep.Address);
						Console.WriteLine("Returning Response...");
						udp.Send(resp, resp.Length, ep);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
				}

#if !ASYNC
				udp.BeginReceive(UdpReceived, ar.AsyncState); // infinite async
#endif
			}
			catch { }
		}

		private Cache _cache = new Cache();

		bool IsMyIp(IPAddress address)
		{
			return _myAddress.Contains(address);
		}

		byte[] Handle(byte[] data, IPAddress from)
		{
			// There is 3 ways
			// 1 - it is served from cache
			// 2 - it is delegated/forwarded to another server
			// 3 - it is served from cache but since TTL outdated a new background update request issued

			var req = Request.Parse(data);
			Console.WriteLine(req.ToString());
			if ((req.Header.Flags & HeaderFlags.Truncation) > 0)
			{
				Console.WriteLine(ConsoleColor.Red, "TRUNCATED");
			}

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
						if (cachedResult.IsOutdated)
						{
							Task.Run(delegate
							{
								using (ConsoleUtil.UseColor(ConsoleColor.Yellow))
								{
									Console.WriteLine("Requesting updated record...");
								}
								Forward(data, from);
							}); // Forwarder must update the cache, but meanwhile we will respond immediately with outdated answer

							// also reset ttl
							response.Answers.ForEach(x => x.Ttl = 0);
							response.AuthorityRR.ForEach(x => x.Ttl = 0);
							response.AdditionalRR.ForEach(x => x.Ttl = 0);
						}

						const int minTtl = 0; // seconds

						// patch TTL with the time remaining since cached?
						var secondsAgo = (int)(DateTime.UtcNow - cachedResult.CachedAtUtc).TotalSeconds;
						response.Answers.ForEach(x => x.Ttl = (int)Math.Min(minTtl, x.Ttl - secondsAgo));
						response.AuthorityRR.ForEach(x => x.Ttl = (int)Math.Min(minTtl, x.Ttl - secondsAgo));
						response.AdditionalRR.ForEach(x => x.Ttl = (int)Math.Min(minTtl, x.Ttl - secondsAgo));

						response.Questions = req.Questions;
						response.Header.Identifiation = req.Header.Identifiation;

						using (ConsoleUtil.UseColor(ConsoleColor.Green))
						{
							Console.WriteLine("Hit Cache!");
							Console.WriteLine(response);
						}

						return response.Serialzie();
					}
				}

			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
			Console.WriteLine();

			if (req.Questions.Count == 1)
			{
				var query = req.Questions[0];

				var sptrData = HandleSelfPRT(req, query, from);
				if (sptrData != null)
				{
					return sptrData;
				}

				var selfName = HandleSelfName(req, query, from);
				if (selfName != null)
				{
					return selfName;
				}

				var localName = HandleLocal(req, query, from);
				if (localName != null)
				{
					return localName;
				}

			}

			using (ConsoleUtil.UseColor(ConsoleColor.Red))
			{
				Console.WriteLine("Cache miss, direct request...");
			}

			return Forward(data, from);
		}

		static IPAddress MyLocalAddressFor(IPAddress from)
		{
			var fromAddrInt = BitConverter.ToUInt32(from.GetAddressBytes().Reverse().ToArray(), 0);

			foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
			{
				var adapterProperties = networkInterface.GetIPProperties();
				foreach (var item in adapterProperties.UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork))
				{
					var myAddrInt = BitConverter.ToUInt32(item.Address.GetAddressBytes().Reverse().ToArray(), 0);
					var maskInt = BitConverter.ToUInt32(item.IPv4Mask.GetAddressBytes().Reverse().ToArray(), 0);
					var network = myAddrInt & maskInt;
					var fromNetwork = fromAddrInt & maskInt;

					if (fromNetwork == network)
					{
						return item.Address;
					}
				}
			}

			return null;
		}

		private byte[] HandleSelfName(Request req, RequestQuestion query, IPAddress from)
		{
			if ((query.Type == RecordType.A) && query.Class == RecordClass.IN)
			{
				if (Environment.MachineName.ToUpperInvariant() == query.Name.ToUpperInvariant())
				{
					// should respond with ip from specific interface
					var local = MyLocalAddressFor(from);
					if (local != null)
					{
						var resp = new Response
						{
							Header =
									{
										Identifiation = req.Header.Identifiation,
										Flags = HeaderFlags.IsResponse,
									},
							Questions =
									{
										query,
									},
							Answers =
									{
										new ResponseRecord(query.Type, query.Name, local.GetAddressBytes(), 5 * 60), // 5 min
									},
						};
						_cache.Set(resp, new IPEndPoint(local, 53));
						return resp.Serialzie();
					}
				}
			}
			return null;
		}
		private byte[] HandleLocal(Request req, RequestQuestion query, IPAddress from)
		{
			if ((query.Type == RecordType.A || query.Type == RecordType.AAAA) && query.Class == RecordClass.IN)
			{
				if (!query.Name.Contains('.'))
				{
					var resp = new Response
					{
						Header =
						{
							Identifiation = req.Header.Identifiation,
							Flags = HeaderFlags.IsResponse | HeaderFlags.RCode_NoSuchName,
						},
						Questions =
						{
							query,
						},
					};
					return resp.Serialzie();
				}
			}
			return null;
		}

		private byte[] HandleSelfPRT(Request req, RequestQuestion query, IPAddress from)
		{
			if (query.Type == RecordType.PTR && query.Class == RecordClass.IN) // PTR to me
			{
				var rx = new Regex(@"(?<adr>(\d+\.){4})in-addr.arpa");
				var match = rx.Match(query.Name);
				if (match.Success)
				{
					var adr = match.Groups["adr"].Value;
					var adrParts = adr.Split('.').Take(4);
					var ip = new IPAddress(adrParts.Reverse().Select(x => byte.Parse(x)).ToArray());
					if (IsMyIp(ip))
					{
						Response resp = new ResponseRecord(RecordType.PTR, query.Name, Response.SerializeHost(Environment.MachineName), int.MaxValue);

						Console.WriteLine(ConsoleColor.Cyan, "Just made up:" + resp);
						_cache.Set(resp, new IPEndPoint(ip, 53));
						return resp.Serialzie();
					}
				}
			}
			return null;
		}
		
		public byte[] Forward(byte[] data, IPAddress from)
		{
			// todo ask all at the same time to reduce delay
			var epi = 0;
			retry:
			IPAddress ep;
			lock (_forwardTo)
			{
				ep = _forwardTo[epi];
			}

			using (var udp = new UdpClient(ep.AddressFamily))
			{
				Console.WriteLine($">>> Forward to {ep}");
				var ei = new IPEndPoint(ep, 53);
				udp.Connect(ei);
				udp.Send(data, data.Length);

				var rTask = udp.ReceiveAsync();
				if (!rTask.Wait(1500))
				{
					lock (_forwardTo)
					{
						var old = _forwardTo[epi];
						epi++;
						if (epi >= _forwardTo.Count)
						{
							var noResp = Response.Parse(data);
							noResp.Header.Flags |= HeaderFlags.IsResponse | (HeaderFlags)ResponseCode.NoSuchName;

							_cache.Set(noResp, new IPEndPoint(MyLocalAddressFor(from), 53));
							return noResp.Serialzie();
							//throw new Exception("Not found");
						}

						Console.WriteLine($"DNS Server {old} is not responding, switching to {_forwardTo[epi]}");
					}

					goto retry;
				}

				// done, make sure it is prioritized
				if (epi != 0)
				{
					lock (_forwardTo)
					{
						_forwardTo.RemoveAt(epi);
						_forwardTo.Insert(0, ep);
						Console.WriteLine($"Moved on top: {ep}");
					}
				}

				var resp = rTask.Result.Buffer;
				Console.WriteLine($"<<< received from {rTask.Result.RemoteEndPoint}");

				try
				{
					var res = Response.Parse(resp);

					#region print

					using (ConsoleUtil.UseColor(ConsoleColor.DarkMagenta))
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
				}
				catch (Exception ex)
				{
					Console.WriteLine(ConsoleColor.Red, ex);
				}

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
