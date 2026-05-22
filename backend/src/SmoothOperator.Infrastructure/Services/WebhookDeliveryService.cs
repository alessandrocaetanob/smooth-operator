using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.Features.Webhooks;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Background worker that drains the <see cref="WebhookDelivery"/> outbox:
    /// it polls for due deliveries, POSTs each to its endpoint with an
    /// HMAC-SHA256 signature, and applies exponential-backoff retry. Modelled on
    /// <see cref="AuditRetentionService"/>.
    /// </summary>
    public class WebhookDeliveryService : BackgroundService
    {
        // Long enough that short-lived integration-test hosts never reach the
        // first poll; real deployments run continuously so this is harmless.
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(6);
        private const int MaxErrorLength = 1000;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEncryptionService _encryption;
        private readonly IHostEnvironment _environment;
        private readonly WebhookOptions _options;
        private readonly ILogger<WebhookDeliveryService> _logger;

        private DateTime _lastPurgeUtc = DateTime.MinValue;

        public WebhookDeliveryService(
            IServiceScopeFactory scopeFactory,
            IHttpClientFactory httpClientFactory,
            IEncryptionService encryption,
            IHostEnvironment environment,
            IOptions<WebhookOptions> options,
            ILogger<WebhookDeliveryService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _encryption = encryption;
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
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Webhook delivery batch failed");
                }

                if (DateTime.UtcNow - _lastPurgeUtc > PurgeInterval)
                {
                    try
                    {
                        await PurgeOldDeliveriesAsync(stoppingToken);
                        _lastPurgeUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Webhook delivery purge failed");
                    }
                }

                try
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task ProcessBatchAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var due = await db.WebhookDeliveries
                .Include(d => d.Endpoint)
                .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= now)
                .OrderBy(d => d.NextAttemptAt)
                .Take(_options.BatchSize)
                .ToListAsync(ct);

            if (due.Count == 0)
                return;

            foreach (var delivery in due)
            {
                if (ct.IsCancellationRequested)
                    break;
                await AttemptDeliveryAsync(delivery, ct);
            }

            // Persist outcomes even on shutdown so a delivered webhook is not re-sent.
            await db.SaveChangesAsync(CancellationToken.None);
        }

        private async Task AttemptDeliveryAsync(WebhookDelivery delivery, CancellationToken ct)
        {
            var endpoint = delivery.Endpoint;
            delivery.AttemptCount++;

            string secret;
            try
            {
                secret = _encryption.Decrypt(endpoint.EncryptedSecret);
            }
            catch (Exception ex)
            {
                MarkFailed(delivery, endpoint, null, $"Secret decryption failed: {ex.Message}");
                return;
            }

            var timestamp = DateTime.UtcNow.ToString("o");
            var signature = WebhookSignature.Compute(secret, timestamp, delivery.Payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Event", delivery.EventType);
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Delivery", delivery.Id.ToString());
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Timestamp", timestamp);
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Signature", signature);

            var client = _httpClientFactory.CreateClient("webhooks");

            try
            {
                using var response = await client.SendAsync(request, ct);
                var code = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    delivery.Status = WebhookDeliveryStatus.Delivered;
                    delivery.DeliveredAt = DateTime.UtcNow;
                    delivery.LastResponseCode = code;
                    delivery.LastError = null;
                    MarkEndpointSuccess(endpoint, code);
                }
                else
                {
                    MarkFailed(delivery, endpoint, code, $"HTTP {code} {response.ReasonPhrase}");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Host is shutting down — leave the row Pending without burning an attempt.
                delivery.AttemptCount--;
            }
            catch (Exception ex)
            {
                // Includes per-request HttpClient timeouts (TaskCanceledException
                // when our own stopping token is not cancelled).
                MarkFailed(delivery, endpoint, null, ex.Message);
            }
        }

        private void MarkFailed(WebhookDelivery delivery, WebhookEndpoint endpoint, int? code, string error)
        {
            delivery.LastResponseCode = code;
            delivery.LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

            if (delivery.AttemptCount >= _options.MaxAttempts)
            {
                delivery.Status = WebhookDeliveryStatus.Dead;
            }
            else
            {
                var backoffSeconds = _options.RetryBaseSeconds * Math.Pow(2, delivery.AttemptCount - 1);
                delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
            }

            endpoint.LastDeliveryAt = DateTime.UtcNow;
            endpoint.LastDeliveryStatus = "failed";
            endpoint.LastResponseCode = code;
            endpoint.ConsecutiveFailures++;
        }

        private static void MarkEndpointSuccess(WebhookEndpoint endpoint, int code)
        {
            endpoint.LastDeliveryAt = DateTime.UtcNow;
            endpoint.LastDeliveryStatus = "success";
            endpoint.LastResponseCode = code;
            endpoint.ConsecutiveFailures = 0;
        }

        private async Task PurgeOldDeliveriesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-_options.DeliveryRetentionDays);
            var deleted = await db.WebhookDeliveries
                .Where(d => (d.Status == WebhookDeliveryStatus.Delivered
                             || d.Status == WebhookDeliveryStatus.Dead)
                            && d.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0 && _logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "Webhook delivery purge removed {Count} rows older than {Cutoff:o}", deleted, cutoff);
        }
    }
}
