using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using qDNS.Model;

namespace qDNS
{
    class DnsServer
    {
        static void Main()
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

            var udp1 = new UdpClient(53, AddressFamily.InterNetwork);
            udp1.BeginReceive(UdpReceived, udp1);

            var udp2 = new UdpClient(53, AddressFamily.InterNetworkV6);
            udp2.BeginReceive(UdpReceived, udp2);

            Console.ReadLine();
        }

        private static readonly List<IPAddress> _address = new List<IPAddress>();

        private static void UdpReceived(IAsyncResult ar)
        {
            var udp = (UdpClient)ar.AsyncState;
            udp.BeginReceive(UdpReceived, ar.AsyncState); // infinite async

            try
            {
                IPEndPoint ep = null;
                var data = udp.EndReceive(ar, ref ep);
                Console.WriteLine($"Received Request: {data.Length} bytes:");

                Console.WriteLine(string.Join("", data.Select(x => x.ToString("X2"))));

                var resp = HandleOrForward(data);
                Console.WriteLine("Returning Response...");
                udp.Send(resp, resp.Length, ep);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static byte[] HandleOrForward(byte[] data)
        {
            try
            {
                var req = Request.Parse(data);
                Console.WriteLine(JsonConvert.SerializeObject(req, Formatting.Indented));

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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            // forward
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

                var rtask = udp.ReceiveAsync();
                if (!rtask.Wait(1500))
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
                // make sure it is prioritized
                if (epi != 0)
                {
                    lock (_address)
                    {
                        _address.RemoveAt(epi);
                        _address.Insert(0, ep);
                        Console.WriteLine($"Moved on top: {ep}");
                    }
                }

                var resp = rtask.Result.Buffer;
                Console.WriteLine($"<<< received from {rtask.Result.RemoteEndPoint}");
/*
                var f = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                try
                {
                    Console.WriteLine(string.Join("", resp.Select(x => x.ToString("X2"))));
                    Console.WriteLine(JsonConvert.SerializeObject(Request.Parse(resp), Formatting.Indented));
                }
                catch (Exception ex){
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                }
                Console.ForegroundColor = f;
*/
                return resp;
            }

        }

    }
}
