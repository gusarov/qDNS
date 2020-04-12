﻿using qDNS.Model;
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
		public int Type;
		public int Class;

		public QueryKey(string query, int type, int @class)
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
		public void Set(QueryKey query, Response res, IPEndPoint receivedFrom)
		{
			_cache[query] = new CacheEntry
			{
				CachedAtUtc = DateTime.UtcNow,
				Response = res,
				ReceivedFrom = receivedFrom,
			};
		}

		public void Set(Response res, IPEndPoint receivedFrom)
		{
			if (res is null)
			{
				throw new ArgumentNullException(nameof(res));
			}

			var query = GetCacheableQuery(res);
			if (query.NotDefault)
			{
				Set(query, res, receivedFrom);
			}
		}

		public static QueryKey GetCacheableQuery(Request req)
		{
			// if ((req.Header.Flags & HeaderFlags.IsResponse) != 0 && req.Questions.Count == 1)
			if (req.Questions.Count == 1)
			{
				return new QueryKey(req.Questions[0].Name, req.Questions[0].Type, req.Questions[0].Class);
			}

			return default;
		}

	}

	class CacheEntry
	{
		public Response Response { get; set; }
		public IPEndPoint ReceivedFrom { get; set; }

		public DateTime CachedAtUtc { get; set; }

		public bool IsOutdated
		{
			get
			{
				var now = DateTime.UtcNow;
				if (Response.Answers.Count == 0)
				{
					return (now - CachedAtUtc).TotalMinutes > 1; // negative cache!
				}

				return Response.Answers.Any(x => CachedAtUtc.AddMinutes(x.Ttl) < now);
			}
		}
	}
}
