using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services.Recording
{
    /// <summary>
    /// Once-a-day sweep that deletes recordings older than the per-vault retention
    /// (falling back to <c>RecordingStorageSettings.RetentionDays</c>). 0 = keep forever.
    /// </summary>
    public sealed class RecordingRetentionHostedService : BackgroundService
    {
        private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RecordingRetentionHostedService> _logger;

        public RecordingRetentionHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<RecordingRetentionHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a minute on boot so startup migrations finish first.
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Recording retention sweep failed");
                }

                try { await Task.Delay(SweepInterval, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task SweepOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var factory = scope.ServiceProvider.GetRequiredService<IRecordingStorageFactory>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var settings = await db.RecordingStorageSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var defaultRetention = settings?.RetentionDays ?? 0;

            var candidates = await db.Recordings
                .Where(r => r.Status == RecordingStatus.Available && r.EndedAt != null)
                .Include(r => r.Connection!).ThenInclude(c => c.ConnectionGroup)
                .ToListAsync(ct);

            if (candidates.Count == 0) return;

            var storage = await factory.CreateAsync(ct);
            var now = DateTime.UtcNow;
            int deleted = 0;

            foreach (var rec in candidates)
            {
                if (ct.IsCancellationRequested) break;

                var retention = rec.Connection?.ConnectionGroup?.RecordingRetentionDays ?? defaultRetention;
                if (retention <= 0) continue; // keep forever
                if (!rec.EndedAt.HasValue) continue;
                if (now - rec.EndedAt.Value < TimeSpan.FromDays(retention)) continue;

                try
                {
                    await storage.DeleteAsync(rec.StorageKey, ct);
                    rec.Status = RecordingStatus.Deleted;
                    await db.SaveChangesAsync(ct);
                    await audit.WriteAsync("recording.retention_deleted", "Recording", rec.Id.ToString(), new
                    {
                        rec.SessionId,
                        rec.ConnectionId,
                        ageDays = (int)(now - rec.EndedAt.Value).TotalDays,
                        retention,
                    });
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete expired recording {RecordingId} from storage", rec.Id);
                }
            }

            if (deleted > 0)
                _logger.LogInformation("Recording retention sweep removed {Count} expired recordings", deleted);
        }
    }
}
