#define ASYNC
#define StrictTtl

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
					var arg = args[i].TrimStart(new[] { '-', '/' });
					switch (arg.ToUpperInvariant())
					{
						case "FWD":
							srv.AddForwarding(IPAddress.Parse(args[++i]));
							break;
						case "SIELENT":
							Console.Enable = false;
							break;
						default:
							if (IPAddress.TryParse(args[i], out var ip))
							{
								var name = args[++i];
								Response resp = new ResponseRecord(name, ip, 24 * 60 * 60); // limit this for client to reduce client leaks
								// resp.Header.Flags |= HeaderFlags.AuthoritativeAnswer;
								srv._cache.Set(resp, default);
							}
							break;
					}
				}

				srv.Run();
				Console.ReadLine();
			}
		}

		public DnsServer()
		{
			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			var fwdAddresses = new HashSet<IPAddress>();
			_myAddress.Add(new IPAddress(new byte[] {127, 0, 0, 1}));
			_myAddress.Add(IPAddress.Parse("::1"));
			foreach (var networkInterface in interfaces)
			{

				var adapterProperties = networkInterface.GetIPProperties();
				foreach (var item in adapterProperties.UnicastAddresses.Select(x => x.Address))
				{
					_myAddress.Add(item);
				}

				var dnsServers = adapterProperties.DnsAddresses;
				if (dnsServers.Count > 0)
				{
					Console.WriteLine(networkInterface.Description);
					foreach (var dns in dnsServers)
					{
						Console.WriteLine($"  DNS Servers: {dns}");
						fwdAddresses.Add(dns);
					}

					Console.WriteLine();
				}
			}
			fwdAddresses.Add(new IPAddress(new byte[] { 1, 1, 1, 1 })); // Cloudflare
			fwdAddresses.Add(new IPAddress(new byte[] { 8, 8, 8, 8 })); // Google

			foreach (var myAddress in _myAddress)
			{
				fwdAddresses.Remove(myAddress);
			}

			_forwardTo.AddRange(fwdAddresses.OrderBy(x => x.AddressFamily));
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

						var ctx = new RequestContext
						{
							RequestData = data,
							From = ep,
						};

						Handle(ctx);
						Console.WriteLine("Returning Response...");
						if (ctx.ResponseData != null)
						{
							if (ctx.Response != null)
							{
								Console.WriteLine("Response preconstructed");
							}
							udp.Send(ctx.ResponseData, ctx.ResponseData.Length, ep);
						}
						else
						{
							Console.WriteLine("No Response Data!");
						}
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

		void Handle(RequestContext ctx)
		{
			// There is 3 ways
			// 1 - it is served from cache
			// 2 - it is delegated/forwarded to another server
			// 3 - it is served from cache but since TTL outdated a new background update request issued

			var req = ctx.Request = Request.Parse(ctx.RequestData);
			bool fromCache = false;
			if (ctx.Request.Questions.Count == 1)
			{
				ctx.Query = ctx.Request.Questions[0];
			}

			try
			{
				Console.WriteLine(ctx.Request.ToString());
				if ((ctx.Request.Header.Flags & HeaderFlags.Truncation) > 0)
				{
					Console.WriteLine(ConsoleColor.Red, "TRUNCATED");
				}

				try
				{
					// usually it have only one dns host request
					var name = Cache.GetCacheableQuery(ctx.Request);
					if (name.NotDefault)
					{
						var cachedResult = _cache.Get(name);
						if (cachedResult != null)
						{
							if (cachedResult.IsOutdated)
							{
								Task.Run(delegate
								{
									using (ConsoleUtil.UseColor(ConsoleColor.Yellow))
									{
										Console.WriteLine("Requesting updated record...");
									}
									Forward(new RequestContext
									{
										From = ctx.From,
										RequestData = ctx.RequestData,
										Request = ctx.Request,
									});
								}); // Forwarder must update the cache, but meanwhile we will respond immediately with outdated answer
							}

							const int minTtl = 0; // seconds

							// patch TTL with the time remaining since cached?
							using (ConsoleUtil.UseColor(ConsoleColor.Green))
							{
								Console.WriteLine("Hit Cache!");
#if StrictTtl
								var secondsAgo = (int)(DateTime.UtcNow - cachedResult.CachedAtUtc).TotalSeconds;
								var newResponse = cachedResult.Response.Clone();
								newResponse.Answers.ForEach(x => x.Ttl = (int)Math.Min(minTtl, x.Ttl - secondsAgo));
								newResponse.AuthorityRR.ForEach(x => x.Ttl = (int)Math.Min(minTtl, x.Ttl - secondsAgo));
								newResponse.AdditionalRR.ForEach(x => x.Ttl = (int)Math.Min(minTtl, x.Ttl - secondsAgo));

								newResponse.Questions = ctx.Request.Questions;
								newResponse.Header.Identifiation = ctx.Request.Header.Identifiation;
								ctx.Response = newResponse;
								ctx.ResponseData = newResponse.Serialize();
								Console.WriteLine(newResponse);
#else
								// var buf = cachedResult.ResponseData;
								var buf = new byte[cachedResult.ResponseData.Length];
								cachedResult.ResponseData.CopyTo(buf, 0);

								buf[0] = ctx.RequestData[0];
								buf[1] = ctx.RequestData[1];
								buf[2] = ctx.RequestData[2];
								buf[3] = ctx.RequestData[3];
								ctx.ResponseData = buf;
#endif
								fromCache = true;
							}

							return;
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
					// var query = req.Questions[0];

					if (HandleSelfPRT(ctx))
					{
						return;
					}
					if (HandleSelfName(ctx))
					{
						return;
					}
					if (HandleLocal(ctx))
					{
						return;
					}
				}

				using (ConsoleUtil.UseColor(ConsoleColor.Red))
				{
					Console.WriteLine("Cache miss, direct request...");
				}

				Forward(ctx);
			}
			finally
			{
				try
				{
					#region print

					using (ConsoleUtil.UseColor(ConsoleColor.DarkMagenta))
					{
						Console.WriteLine(string.Join("", ctx.ResponseData.Select(x => x.ToString("X2"))));

						if (ctx.Response != null && ctx.ResponseData == null)
						{
							ctx.ResponseData = ctx.Response.Serialize();
						}

						if (!fromCache && (ctx.Response != null || ctx.ResponseData != null))
						{
							if (ctx.Response == null && ctx.ResponseData != null)
							{
								ctx.Response = Response.Parse(ctx.ResponseData);
							}

							try
							{
								Console.WriteLine(ctx.Response);
								Console.WriteLine(JsonConvert.SerializeObject(ctx.Response, Formatting.Indented));
							}
							catch (Exception ex)
							{
								Console.WriteLine(ex.Message);
							}

							ctx.Response.Header.Flags |= HeaderFlags.IsResponse;

							_cache.Set(ctx.Response, ctx.ResponseFrom);
						}
					}

					#endregion

				}
				catch (Exception ex)
				{
					Console.WriteLine(ConsoleColor.Red, ex);
				}

			}
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

		private bool HandleSelfName(RequestContext ctx)
		{
			var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;

			RequestQuestion query;
			if (ctx.Request.Questions.Count == 1)
			{
				query = ctx.Request.Questions[0];
			}
			else
			{
				return false;
			}

			if ((query.Type == RecordType.A) && query.Class == RecordClass.IN)
			{
				var hostName = Environment.MachineName;
				var fqdn = Environment.MachineName + "." + domain;

				if (string.Equals(query.Name, hostName, StringComparison.OrdinalIgnoreCase)
				    || string.Equals(query.Name, fqdn, StringComparison.OrdinalIgnoreCase))
				{
					// should respond with ip from specific interface
					var local = MyLocalAddressFor(ctx.From.Address);
					if (local != null)
					{
						var resp = new Response
						{
							Header =
									{
										Identifiation = ctx.Request.Header.Identifiation,
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
						ctx.Response = resp;
						ctx.ResponseData = resp.Serialize();
						return true;
					}
				}
			}
			return false;
		}
		private bool HandleLocal(RequestContext ctx)
		{
			var query = ctx.Query;

			if ((query.Type == RecordType.A || query.Type == RecordType.AAAA) && query.Class == RecordClass.IN)
			{
				if (!query.Name.Contains('.'))
				{
					var resp = new Response
					{
						Header =
						{
							Identifiation = ctx.Request.Header.Identifiation,
							Flags = HeaderFlags.IsResponse | HeaderFlags.RCode_NoSuchName,
						},
						Questions =
						{
							query,
						},
					};
					ctx.Response = resp;
					ctx.ResponseData = resp.Serialize();
					return true;
				}
			}
			return false;
		}

		private bool HandleSelfPRT(RequestContext ctx)
		{
			var query = ctx.Query;
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
						ctx.Response = resp;
						ctx.ResponseData = resp.Serialize();
						return true;
					}
				}
			}
			return false;
		}

		public bool Forward(RequestContext ctx)
		{
			var query = ctx.Query;

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
				udp.Send(ctx.RequestData, ctx.RequestData.Length);

				var rTask = udp.ReceiveAsync();
				if (!rTask.Wait(1500))
				{
					lock (_forwardTo)
					{
						var old = _forwardTo[epi];
						epi++;
						if (epi >= _forwardTo.Count)
						{
							var noResp = Response.Parse(ctx.RequestData);
							noResp.Header.Flags |= HeaderFlags.IsResponse | (HeaderFlags)ResponseCode.NoSuchName;

							ctx.Response = noResp;
							ctx.ResponseData = noResp.Serialize();
							return false;
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

				ctx.ResponseData = rTask.Result.Buffer;
				ctx.ResponseFrom = rTask.Result.RemoteEndPoint;
				Console.WriteLine($"<<< received from {rTask.Result.RemoteEndPoint}");

				return true;
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

	public class RequestContext
	{
		public IPEndPoint From;
		public byte[] RequestData;
		public Request Request;
		public RequestQuestion Query;

		public IPEndPoint ResponseFrom;
		public byte[] ResponseData;
		public Response Response;
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
