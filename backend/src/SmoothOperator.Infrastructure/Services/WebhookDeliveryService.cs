using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.Options;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Background worker that drains the <see cref="Domain.Models.WebhookDelivery"/>
    /// outbox on a fixed cadence. The actual sending logic lives in
    /// <see cref="WebhookDeliveryProcessor"/> so it can be unit-tested in isolation.
    /// </summary>
    public class WebhookDeliveryService : BackgroundService
    {
        // Long enough that short-lived integration-test hosts never reach the
        // first poll; real deployments run continuously so this is harmless.
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _environment;
        private readonly WebhookOptions _options;
        private readonly ILogger<WebhookDeliveryService> _logger;

        private DateTime _lastPurgeUtc = DateTime.MinValue;

        public WebhookDeliveryService(
            IServiceScopeFactory scopeFactory,
            IHostEnvironment environment,
            IOptions<WebhookOptions> options,
            ILogger<WebhookDeliveryService> logger)
        {
            _scopeFactory = scopeFactory;
            _environment = environment;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Never deliver webhooks during the integration-test run.
            if (_environment.IsEnvironment("Testing"))
                return;

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (TaskCanceledException) { return; }

            var pollInterval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                await DrainOnceAsync(stoppingToken);

                if (DateTime.UtcNow - _lastPurgeUtc > PurgeInterval)
                {
                    await PurgeOnceAsync(stoppingToken);
                    _lastPurgeUtc = DateTime.UtcNow;
                }

                try
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task DrainOnceAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<WebhookDeliveryProcessor>();
                await processor.ProcessBatchAsync(db, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Webhook delivery batch failed");
            }
        }

        private async Task PurgeOnceAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<WebhookDeliveryProcessor>();
                await processor.PurgeOldAsync(db, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Webhook delivery purge failed");
            }
        }
    }
}
