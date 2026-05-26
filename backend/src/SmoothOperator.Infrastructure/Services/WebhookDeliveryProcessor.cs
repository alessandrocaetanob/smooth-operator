using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    /// Stateless delivery engine driven by <see cref="WebhookDeliveryService"/>.
    /// Loading and persisting use the caller-supplied <see cref="AppDbContext"/>
    /// (one per batch); HTTP sends fan out in parallel so a few slow receivers
    /// don't gate the rest of the batch.
    /// </summary>
    public class WebhookDeliveryProcessor
    {
        public const string HttpClientName = "webhooks";

        private const int MaxErrorLength = 1000;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEncryptionService _encryption;
        private readonly WebhookOptions _options;
        private readonly ILogger<WebhookDeliveryProcessor> _logger;

        public WebhookDeliveryProcessor(
            IHttpClientFactory httpClientFactory,
            IEncryptionService encryption,
            IOptions<WebhookOptions> options,
            ILogger<WebhookDeliveryProcessor> logger)
        {
            _httpClientFactory = httpClientFactory;
            _encryption = encryption;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Drains a single batch of due deliveries. Returns the number of rows
        /// processed (delivered, marked Dead, or rescheduled).
        /// </summary>
        public async Task<int> ProcessBatchAsync(AppDbContext db, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var due = await db.WebhookDeliveries
                .Include(d => d.Endpoint)
                .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= now)
                .OrderBy(d => d.NextAttemptAt)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            if (due.Count == 0)
                return 0;

            // Decrypt each endpoint's secret at most once per batch.
            // Use Lazy<T> wrapper because ConcurrentDictionary.GetOrAdd's value factory
            // can run more than once under contention — wrapping in Lazy guarantees
            // single-execution semantics for the underlying Decrypt call.
            var decryptedByEndpoint = new ConcurrentDictionary<Guid, Lazy<string?>>();
            string? ResolveSecret(WebhookEndpoint endpoint) =>
                decryptedByEndpoint.GetOrAdd(endpoint.Id,
                    _ => new Lazy<string?>(() => TryDecrypt(endpoint.EncryptedSecret), LazyThreadSafetyMode.ExecutionAndPublication))
                    .Value;

            var results = new ConcurrentDictionary<Guid, AttemptResult>();

            await Parallel.ForEachAsync(
                due,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = _options.MaxConcurrency,
                },
                async (delivery, ct) =>
                {
                    var secret = ResolveSecret(delivery.Endpoint);
                    var result = await SendAsync(delivery, secret, ct);
                    results[delivery.Id] = result;
                });

            // Apply outcomes serially on the single (non-thread-safe) DbContext.
            foreach (var delivery in due)
            {
                if (!results.TryGetValue(delivery.Id, out var result))
                    continue; // shutdown cancelled this one before it was sent
                ApplyResult(delivery, result);
            }

            // Persist results even on shutdown so a delivered webhook is not re-sent.
            await db.SaveChangesAsync(CancellationToken.None);
            return due.Count;
        }

        /// <summary>Removes Delivered / Dead rows older than the retention window.</summary>
        public async Task<int> PurgeOldAsync(AppDbContext db, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.DeliveryRetentionDays);
            var deleted = await db.WebhookDeliveries
                .Where(d => (d.Status == WebhookDeliveryStatus.Delivered
                             || d.Status == WebhookDeliveryStatus.Dead)
                            && d.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0 && _logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "Webhook delivery purge removed {Count} rows older than {Cutoff:o}", deleted, cutoff);

            return deleted;
        }

        // --- internals ----------------------------------------------------

        private string? TryDecrypt(string encryptedSecret)
        {
            try
            {
                return _encryption.Decrypt(encryptedSecret);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt webhook signing secret");
                return null;
            }
        }

        private async Task<AttemptResult> SendAsync(WebhookDelivery delivery, string? secret, CancellationToken ct)
        {
            if (secret is null)
                return AttemptResult.Failed(null, "Secret decryption failed.");

            var timestamp = DateTime.UtcNow.ToString("o");
            var signature = WebhookSignature.Compute(secret, timestamp, delivery.Payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Endpoint.Url)
            {
                Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Event", delivery.EventType);
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Delivery", delivery.Id.ToString());
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Timestamp", timestamp);
            request.Headers.TryAddWithoutValidation("X-SmoothOperator-Signature", signature);

            var client = _httpClientFactory.CreateClient(HttpClientName);

            try
            {
                using var response = await client.SendAsync(request, ct);
                var code = (int)response.StatusCode;
                return response.IsSuccessStatusCode
                    ? AttemptResult.Success(code)
                    : AttemptResult.Failed(code, $"HTTP {code} {response.ReasonPhrase}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Host is shutting down — don't burn an attempt.
                return AttemptResult.Aborted;
            }
            catch (Exception ex)
            {
                return AttemptResult.Failed(null, ex.Message);
            }
        }

        private void ApplyResult(WebhookDelivery delivery, AttemptResult result)
        {
            if (result.Outcome == AttemptOutcome.Aborted)
                return; // leave Pending with the same NextAttemptAt

            delivery.AttemptCount++;
            var endpoint = delivery.Endpoint;

            if (result.Outcome == AttemptOutcome.Success)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.LastResponseCode = result.Code;
                delivery.LastError = null;
                endpoint.LastDeliveryAt = DateTime.UtcNow;
                endpoint.LastDeliveryStatus = "success";
                endpoint.LastResponseCode = result.Code;
                endpoint.ConsecutiveFailures = 0;
                return;
            }

            delivery.LastResponseCode = result.Code;
            delivery.LastError = Truncate(result.Error ?? "Unknown error");

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
            endpoint.LastResponseCode = result.Code;
            endpoint.ConsecutiveFailures++;
        }

        private static string Truncate(string value) =>
            value.Length > MaxErrorLength ? value[..MaxErrorLength] : value;

        // --- value type for the parallel-send / serial-apply hand-off ----

        internal enum AttemptOutcome { Success, Failed, Aborted }

        internal readonly record struct AttemptResult(AttemptOutcome Outcome, int? Code, string? Error)
        {
            public static AttemptResult Success(int code) => new(AttemptOutcome.Success, code, null);
            public static AttemptResult Failed(int? code, string error) => new(AttemptOutcome.Failed, code, error);
            public static AttemptResult Aborted { get; } = new(AttemptOutcome.Aborted, null, null);
        }
    }
}
