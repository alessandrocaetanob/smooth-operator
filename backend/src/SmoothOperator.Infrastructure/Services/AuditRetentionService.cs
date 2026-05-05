using System;
using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Background worker that purges audit log entries older than the configured retention window.
    /// Runs once shortly after startup, then every 24 hours. A retention value of 0 disables purging
    /// (logs are kept forever — this is the default).
    /// </summary>
    public class AuditRetentionService : BackgroundService
    {
        private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditRetentionService> _logger;

        public AuditRetentionService(IServiceScopeFactory scopeFactory, ILogger<AuditRetentionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Audit retention purge failed");
                }

                try
                {
                    await PurgeExpiredSsoStatesAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Expired SSO auth-state purge failed");
                }

                try
                {
                    await Task.Delay(PurgeInterval, stoppingToken);
                }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task PurgeOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var settings = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var retentionDays = settings?.AuditLogRetentionDays ?? 0;
            if (retentionDays <= 0)
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var deleted = await db.AuditLogs
                .Where(l => l.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Audit retention purged {Count} entries older than {Cutoff:o} (retention={Days}d)",
                    deleted, cutoff, retentionDays);

                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    UserId = null,
                    Action = "system.audit_purged",
                    ResourceType = "AuditLog",
                    ResourceId = string.Empty,
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        deleted,
                        retentionDays,
                        cutoff,
                    }),
                    IpAddress = string.Empty,
                });
                await db.SaveChangesAsync(ct);
            }
        }

        private async Task PurgeExpiredSsoStatesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var deleted = await db.SsoAuthStates
                .Where(s => s.ExpiresAt < now)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "SSO auth-state sweep removed {Count} expired rows older than {Now:o}",
                    deleted, now);
            }
        }
    }
}
