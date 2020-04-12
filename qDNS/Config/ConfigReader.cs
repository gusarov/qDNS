using qDNS.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace qDNS.Config
{
	class ConfigReader
	{
		public Config Read(string file, Cache cache)
		{
			var config = new Config();
			foreach (var line in File.ReadAllLines(file))
			{
				var parts = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 2)
				{
					var addressString = parts[0];
					var address = IPAddress.Parse(addressString);
					var name = parts[1];
					/*
                    cache.Set(new , new Response
                    {
                        // should be filled with original request
                        Answers =
                        {
                            new ResponseRecord
                            {
                                Name = name,
                                Class = 1,
                                Type = 1,
                                Ttl = int.MaxValue - 1, // for safety
                                Data = address.GetAddressBytes(),
                            },
                        },
                    });
					*/
				}
			}

			return config;
		}
	}

	class Config
	{
		public List<IPAddress> ForwardTo { get; set; }
	}
}
