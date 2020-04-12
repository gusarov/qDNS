using qDNS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace qDNS
{
    class Cache
    {
        private readonly Dictionary<Request, CacheEntry> _cache = new Dictionary<Request, CacheEntry>();

        public CacheEntry Get(Request req)
        {
            if (_cache.TryGetValue(req, out var entry))
            {
                return entry;
            }
            return null;
        }

        /// <summary>
        /// User null for negative caching
        /// </summary>
        public void Set([NotNull] Request req, Response res)
        {
            if (req is null)
            {
                throw new ArgumentNullException(nameof(req));
            }

            _cache[req] = new CacheEntry
            {
                CachedAtUtc = DateTime.UtcNow,
                Response = res,
            };
        }
    }

    class CacheEntry
    {
        public Response Response { get; set; }

        public DateTime CachedAtUtc { get; set; }

        public bool IsOutdated
        {
            get
            {
                var now = DateTime.UtcNow;
                return Response.Answers.Any(x => CachedAtUtc.AddMinutes(x.Ttl) < now);
            }
        }
    }
}
