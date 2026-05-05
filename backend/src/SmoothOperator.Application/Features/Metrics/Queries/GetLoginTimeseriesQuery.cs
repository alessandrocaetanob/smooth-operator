using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Metrics.Queries
{
    public sealed record GetLoginTimeseriesQuery(int Hours = 1, int BucketMinutes = 5)
        : IRequest<IEnumerable<TimeseriesBucketDto>>;

    public sealed class GetLoginTimeseriesQueryHandler
        : IRequestHandler<GetLoginTimeseriesQuery, IEnumerable<TimeseriesBucketDto>>
    {
        private readonly IAppDbContext _db;

        public GetLoginTimeseriesQueryHandler(IAppDbContext db) => _db = db;

        public async Task<IEnumerable<TimeseriesBucketDto>> Handle(
            GetLoginTimeseriesQuery request, CancellationToken cancellationToken)
        {
            var hours = Math.Clamp(request.Hours, 1, 168);
            var bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 1440);

            var now = DateTime.UtcNow;
            var since = now.AddHours(-hours);
            var rows = await _db.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since && l.Action.StartsWith("user.login"))
                .Select(l => new { l.Timestamp, l.Outcome })
                .ToListAsync(cancellationToken);

            return BuildTimeseries(rows.Select(r => (r.Timestamp, r.Outcome)), since, now, bucketMinutes);
        }

        internal static IEnumerable<TimeseriesBucketDto> BuildTimeseries(
            IEnumerable<(DateTime Timestamp, string Label)> rows,
            DateTime since,
            DateTime now,
            int bucketMinutes)
        {
            var buckets = new SortedDictionary<int, Dictionary<string, long>>();

            foreach (var (ts, label) in rows)
            {
                var bucketIndex = (int)((ts - since).TotalMinutes / bucketMinutes);
                if (!buckets.TryGetValue(bucketIndex, out var d))
                    buckets[bucketIndex] = d = new Dictionary<string, long>();

                if (!d.TryAdd(label, 1))
                    d[label]++;
            }

            var totalBuckets = (int)Math.Ceiling((now - since).TotalMinutes / bucketMinutes);
            for (var i = 0; i < totalBuckets; i++)
            {
                if (!buckets.ContainsKey(i))
                    buckets[i] = new Dictionary<string, long>();
            }

            return buckets.Select(kv => new TimeseriesBucketDto
            {
                Timestamp = since.AddMinutes(kv.Key * bucketMinutes),
                Values = kv.Value,
            });
        }
    }
}
