using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Metrics.Queries
{
    public sealed record GetMetricsSummaryQuery : IRequest<MetricsSummaryDto>;

    public sealed class GetMetricsSummaryQueryHandler : IRequestHandler<GetMetricsSummaryQuery, MetricsSummaryDto>
    {
        private readonly IAppDbContext _db;
        private readonly IAppMetrics _metrics;

        public GetMetricsSummaryQueryHandler(IAppDbContext db, IAppMetrics metrics)
        {
            _db = db;
            _metrics = metrics;
        }

        public async Task<MetricsSummaryDto> Handle(GetMetricsSummaryQuery request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var since24h = now.AddHours(-24);

            var logs = _db.AuditLogs.AsNoTracking().Where(l => l.Timestamp >= since24h);

            var loginTotal24h = await logs.CountAsync(l => l.Action.StartsWith("user.login"), cancellationToken);
            var loginSuccess24h = await logs.CountAsync(l => l.Action.StartsWith("user.login") && l.Outcome == "success", cancellationToken);
            var loginFailures24h = await logs.CountAsync(l => l.Action.StartsWith("user.login") && l.Outcome == "failure", cancellationToken);
            var connectionsStarted24h = await logs.CountAsync(l => l.Action == "connection.started", cancellationToken);
            var connectionsEnded24h = await logs.CountAsync(l => l.Action == "connection.ended", cancellationToken);
            var auditTotal24h = await logs.CountAsync(cancellationToken);
            var auditFailures24h = await logs.CountAsync(l => l.Outcome == "failure", cancellationToken);
            var authEvents24h = await logs.CountAsync(l => l.Action.StartsWith("user."), cancellationToken);

            return new MetricsSummaryDto
            {
                ActiveSessions = _metrics.CurrentActiveSessions,
                LoginAttemptsTotal24h = loginTotal24h,
                LoginFailures24h = loginFailures24h,
                LoginSuccessRate24h = loginTotal24h > 0
                    ? Math.Round((double)loginSuccess24h / loginTotal24h * 100, 1)
                    : 0,
                ConnectionsStarted24h = connectionsStarted24h,
                ConnectionsEnded24h = connectionsEnded24h,
                AuditEventsTotal24h = auditTotal24h,
                AuditFailures24h = auditFailures24h,
                AuthEvents24h = authEvents24h,
            };
        }
    }
}
