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
    public sealed record GetTopAuditEventsQuery(int Hours = 24, int Limit = 10)
        : IRequest<IEnumerable<TopEventDto>>;

    public sealed class GetTopAuditEventsQueryHandler
        : IRequestHandler<GetTopAuditEventsQuery, IEnumerable<TopEventDto>>
    {
        private readonly IAppDbContext _db;

        public GetTopAuditEventsQueryHandler(IAppDbContext db) => _db = db;

        public async Task<IEnumerable<TopEventDto>> Handle(
            GetTopAuditEventsQuery request, CancellationToken cancellationToken)
        {
            var hours = Math.Clamp(request.Hours, 1, 168);
            var limit = Math.Clamp(request.Limit, 1, 50);

            var since = DateTime.UtcNow.AddHours(-hours);
            return await _db.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since)
                .GroupBy(l => l.Action)
                .Select(g => new TopEventDto { Action = g.Key, Count = g.LongCount() })
                .OrderByDescending(t => t.Count)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
    }
}
