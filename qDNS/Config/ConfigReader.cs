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
            foreach (var VARIABLE in File.ReadAllLines(file))
            {
                
            }
        }
    }

    class Config
    {
        public List<IPAddress> ForwardTo { get; set; }
    }
}
