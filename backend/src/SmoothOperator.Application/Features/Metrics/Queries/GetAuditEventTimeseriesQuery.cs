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
    public sealed record GetAuditEventTimeseriesQuery(int Hours = 24, int BucketMinutes = 60)
        : IRequest<IEnumerable<TimeseriesBucketDto>>;

    public sealed class GetAuditEventTimeseriesQueryHandler
        : IRequestHandler<GetAuditEventTimeseriesQuery, IEnumerable<TimeseriesBucketDto>>
    {
        private readonly IAppDbContext _db;

        public GetAuditEventTimeseriesQueryHandler(IAppDbContext db) => _db = db;

        public async Task<IEnumerable<TimeseriesBucketDto>> Handle(
            GetAuditEventTimeseriesQuery request, CancellationToken cancellationToken)
        {
            var hours = Math.Clamp(request.Hours, 1, 168);
            var bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 1440);

            var now = DateTime.UtcNow;
            var since = now.AddHours(-hours);

            // Limit to the top 5 most frequent actions to keep chart readable
            var topActions = await _db.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since)
                .GroupBy(l => l.Action)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken);

            var rows = await _db.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since && topActions.Contains(l.Action))
                .Select(l => new { l.Timestamp, l.Action })
                .ToListAsync(cancellationToken);

            return GetLoginTimeseriesQueryHandler.BuildTimeseries(
                rows.Select(r => (r.Timestamp, r.Action)), since, now, bucketMinutes);
        }
    }
}
