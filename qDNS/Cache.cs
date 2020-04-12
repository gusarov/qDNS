using qDNS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using System.Net;

namespace qDNS
{
	struct QueryKey : IEquatable<QueryKey>
	{
		public bool NotDefault => Class > 0;
		public string Query;
		public RecordType Type;
		public RecordClass Class;

		public QueryKey(string query, RecordType type, RecordClass @class)
		{
			Query = query;
			Type = type;
			Class = @class;
		}

		public override bool Equals(object obj) => obj is QueryKey key && Equals(key);
		public bool Equals(QueryKey other) => Query == other.Query && Type == other.Type && Class == other.Class;

		public override int GetHashCode()
		{
			var hashCode = -2067533497;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Query);
			hashCode = hashCode * -1521134295 + Type.GetHashCode();
			hashCode = hashCode * -1521134295 + Class.GetHashCode();
			return hashCode;
		}

		public override string ToString() => $"({Type}:{Class}) {Query}";

	}

	class Cache
	{
		private readonly Dictionary<QueryKey, CacheEntry> _cache = new Dictionary<QueryKey, CacheEntry>();

		public CacheEntry Get(QueryKey query)
		{
			if (_cache.TryGetValue(query, out var entry))
			{
				return entry;
			}

			return null;
		}

		/// <summary>
		/// User null for negative caching
		/// </summary>
		public void Set(QueryKey query, Response res, IPEndPoint receivedFrom, bool isStatic = false)
		{
			_cache[query] = new CacheEntry
			{
				CachedAtUtc = isStatic ? new DateTime(4000, 1, 1) : DateTime.UtcNow,
				TtlOverride = isStatic ? 24 * 60 * 60 : default(int?),
				Response = res,
				ReceivedFrom = receivedFrom,
			};
		}

		public void Set(Response res, IPEndPoint receivedFrom, bool isStatic = false)
		{
			if (res is null)
			{
				throw new ArgumentNullException(nameof(res));
			}

			var query = GetCacheableQuery(res);
			if (query.NotDefault)
			{
				Set(query, res, receivedFrom, isStatic: isStatic);
			}
		}

		public static QueryKey GetCacheableQuery(Request req)
		{
			// if ((req.Header.Flags & HeaderFlags.IsResponse) != 0 && req.Questions.Count == 1)
			if (req.Questions.Count == 1
				&& req.Questions[0].Class == RecordClass.IN
				&& (req.Questions[0].Type == RecordType.A
				|| req.Questions[0].Type == RecordType.PTR
				|| req.Questions[0].Type == RecordType.AAAA
				))
			{
				return new QueryKey(req.Questions[0].Name, req.Questions[0].Type, req.Questions[0].Class);
			}
			return default;
		}

	}

	class CacheEntry
	{
		public Response Response { get; set; }

		private byte[] _responseData;
		public byte[] ResponseData
		{
			get
			{
				return _responseData ?? (_responseData = Response.Serialize());
			}
		}
		public IPEndPoint ReceivedFrom { get; set; }

		public DateTime CachedAtUtc { get; set; }

		public int? TtlOverride { get; set; }

		public bool IsOutdated
		{
			get
			{
				var now = DateTime.UtcNow;

				if (TtlOverride.HasValue)
				{
					return CachedAtUtc.AddSeconds(TtlOverride.Value) < now;
				}

				if (ResponseData != null && Response == null)
				{
					Response = Response.Parse(ResponseData);
				}

				if (Response.Answers.Count == 0)
				{
					return (now - CachedAtUtc).TotalMinutes > 1; // negative cache!
				}

				return Response.Answers.Any(x => CachedAtUtc.AddSeconds(x.Ttl) < now);
			}
		}
	}
}
