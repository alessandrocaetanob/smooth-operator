using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Data;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/metrics")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class MetricsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAppMetrics _metrics;

        public MetricsController(AppDbContext context, IAppMetrics metrics)
        {
            _context = context;
            _metrics = metrics;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<MetricsSummaryDto>> GetSummary()
        {
            var now = DateTime.UtcNow;
            var since24h = now.AddHours(-24);
            var since1h = now.AddHours(-1);

            // Compute counts as SQL aggregates rather than materializing all 24h of audit
            // rows in memory. Each query runs as a small COUNT/GROUP BY against the
            // (UserId, Action, ResourceId, Timestamp) index.
            var logs = _context.AuditLogs.AsNoTracking().Where(l => l.Timestamp >= since24h);

            var loginTotal24h = await logs.CountAsync(l => l.Action.StartsWith("user.login"));
            var loginSuccess24h = await logs.CountAsync(l => l.Action.StartsWith("user.login") && l.Outcome == "success");
            var loginFailures1h = await _context.AuditLogs.AsNoTracking()
                .CountAsync(l => l.Timestamp >= since1h && l.Action.StartsWith("user.login") && l.Outcome == "failure");
            var connectionsStarted24h = await logs.CountAsync(l => l.Action == "connection.started");
            var connectionsEnded24h = await logs.CountAsync(l => l.Action == "connection.ended");
            var auditTotal24h = await logs.CountAsync();
            var auditFailures24h = await logs.CountAsync(l => l.Outcome == "failure");
            var authEvents24h = await logs.CountAsync(l => l.Action.StartsWith("user."));

            return Ok(new MetricsSummaryDto
            {
                ActiveSessions = _metrics.CurrentActiveSessions,
                LoginAttemptsTotal24h = loginTotal24h,
                LoginFailures1h = loginFailures1h,
                LoginSuccessRate24h = loginTotal24h > 0
                    ? Math.Round((double)loginSuccess24h / loginTotal24h * 100, 1)
                    : 0,
                ConnectionsStarted24h = connectionsStarted24h,
                ConnectionsEnded24h = connectionsEnded24h,
                AuditEventsTotal24h = auditTotal24h,
                AuditFailures24h = auditFailures24h,
                AuthEvents24h = authEvents24h,
            });
        }

        [HttpGet("logins/timeseries")]
        public async Task<ActionResult<IEnumerable<TimeseriesBucketDto>>> GetLoginTimeseries(
            [FromQuery] int hours = 1,
            [FromQuery] int bucketMinutes = 5)
        {
            hours = Math.Clamp(hours, 1, 168);
            bucketMinutes = Math.Clamp(bucketMinutes, 1, 1440);

            var now = DateTime.UtcNow;
            var since = now.AddHours(-hours);
            var rows = await _context.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since && l.Action.StartsWith("user.login"))
                .Select(l => new { l.Timestamp, l.Outcome })
                .ToListAsync();

            return Ok(BuildTimeseries(rows.Select(r => (r.Timestamp, r.Outcome)), since, now, bucketMinutes));
        }

        [HttpGet("connections/timeseries")]
        public async Task<ActionResult<IEnumerable<TimeseriesBucketDto>>> GetConnectionsTimeseries(
            [FromQuery] int hours = 24,
            [FromQuery] int bucketMinutes = 60)
        {
            hours = Math.Clamp(hours, 1, 168);
            bucketMinutes = Math.Clamp(bucketMinutes, 1, 1440);

            var now = DateTime.UtcNow;
            var since = now.AddHours(-hours);
            var rows = await _context.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since &&
                    (l.Action == "connection.started" || l.Action == "connection.ended"))
                .Select(l => new { l.Timestamp, l.Action })
                .ToListAsync();

            return Ok(BuildTimeseries(rows.Select(r => (r.Timestamp, r.Action)), since, now, bucketMinutes));
        }

        [HttpGet("audit-events/top")]
        public async Task<ActionResult<IEnumerable<TopEventDto>>> GetTopAuditEvents(
            [FromQuery] int hours = 24,
            [FromQuery] int limit = 10)
        {
            hours = Math.Clamp(hours, 1, 168);
            limit = Math.Clamp(limit, 1, 50);

            var since = DateTime.UtcNow.AddHours(-hours);
            var results = await _context.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since)
                .GroupBy(l => l.Action)
                .Select(g => new TopEventDto { Action = g.Key, Count = g.LongCount() })
                .OrderByDescending(t => t.Count)
                .Take(limit)
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("audit-events/breakdown")]
        public async Task<ActionResult<OutcomeBreakdownDto>> GetAuditEventBreakdown(
            [FromQuery] int hours = 24)
        {
            hours = Math.Clamp(hours, 1, 168);

            var since = DateTime.UtcNow.AddHours(-hours);
            var counts = await _context.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since)
                .GroupBy(l => l.Outcome)
                .Select(g => new { Outcome = g.Key, Count = g.LongCount() })
                .ToListAsync();

            return Ok(new OutcomeBreakdownDto
            {
                Success = counts.FirstOrDefault(c => c.Outcome == "success")?.Count ?? 0,
                Failure = counts.FirstOrDefault(c => c.Outcome == "failure")?.Count ?? 0,
            });
        }

        [HttpGet("audit-events/timeseries")]
        public async Task<ActionResult<IEnumerable<TimeseriesBucketDto>>> GetAuditEventTimeseries(
            [FromQuery] int hours = 24,
            [FromQuery] int bucketMinutes = 60)
        {
            hours = Math.Clamp(hours, 1, 168);
            bucketMinutes = Math.Clamp(bucketMinutes, 1, 1440);

            var now = DateTime.UtcNow;
            var since = now.AddHours(-hours);

            // Limit to the top 5 most frequent actions to keep chart readable
            var topActions = await _context.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since)
                .GroupBy(l => l.Action)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToListAsync();

            var rows = await _context.AuditLogs.AsNoTracking()
                .Where(l => l.Timestamp >= since && topActions.Contains(l.Action))
                .Select(l => new { l.Timestamp, l.Action })
                .ToListAsync();

            return Ok(BuildTimeseries(rows.Select(r => (r.Timestamp, r.Action)), since, now, bucketMinutes));
        }

        private static IEnumerable<TimeseriesBucketDto> BuildTimeseries(
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

            // Fill all bucket slots so the frontend gets a continuous x-axis. Use the
            // caller-provided `now` so the bucket count matches the `since` value derived
            // from the same instant — avoids producing an extra trailing empty bucket.
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
