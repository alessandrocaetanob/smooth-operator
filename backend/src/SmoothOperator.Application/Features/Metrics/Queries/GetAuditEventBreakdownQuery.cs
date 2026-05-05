using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Metrics.Queries
{
    public sealed record GetAuditEventBreakdownQuery(int Hours = 24) : IRequest<OutcomeBreakdownDto>;

    public sealed class GetAuditEventBreakdownQueryHandler
        : IRequestHandler<GetAuditEventBreakdownQuery, OutcomeBreakdownDto>
    {
        private readonly IAppDbContext _db;

        public GetAuditEventBreakdownQueryHandler(IAppDbContext db) => _db = db;

        public async Task<OutcomeBreakdownDto> Handle(
            GetAuditEventBreakdownQuery request, CancellationToken cancellationToken)
        {
            var hours = Math.Clamp(request.Hours, 1, 168);

            var since = DateTime.UtcNow.AddHours(-hours);
            var counts = await _db.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since)
                .GroupBy(l => l.Outcome)
                .Select(g => new { Outcome = g.Key, Count = g.LongCount() })
                .ToListAsync(cancellationToken);

            return new OutcomeBreakdownDto
            {
                Success = counts.FirstOrDefault(c => c.Outcome == "success")?.Count ?? 0,
                Failure = counts.FirstOrDefault(c => c.Outcome == "failure")?.Count ?? 0,
            };
        }
    }
}
