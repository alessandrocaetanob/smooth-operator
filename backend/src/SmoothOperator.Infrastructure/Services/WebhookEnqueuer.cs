using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Application.Features.Webhooks;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Transactional-outbox enqueuer: for each enabled endpoint subscribed to an
    /// audit event, adds a <see cref="WebhookDelivery"/> row to the shared scoped
    /// <see cref="AppDbContext"/>. It deliberately does not call SaveChanges — the
    /// originating audit write commits the deliveries in the same transaction.
    /// </summary>
    /// <remarks>
    /// The enabled-endpoint set is served from <see cref="IWebhookEndpointCache"/>
    /// to keep the audit-write hot path off the database.
    /// </remarks>
    public class WebhookEnqueuer : IWebhookEnqueuer
    {
        private readonly AppDbContext _context;
        private readonly IWebhookEndpointCache _cache;

        public WebhookEnqueuer(AppDbContext context, IWebhookEndpointCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task EnqueueAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            var endpoints = await _cache.GetEnabledAsync(cancellationToken);
            if (endpoints.Count == 0)
                return;

            // Build the full set first, then add in one shot, so a fault mid-build
            // cannot leave half-formed delivery rows tracked for the audit commit.
            var deliveries = new List<WebhookDelivery>();
            foreach (var endpoint in endpoints)
            {
                if (!WebhookEventMatcher.Matches(endpoint.EventTypes, entry.Action))
                    continue;

                var deliveryId = Guid.NewGuid();
                deliveries.Add(new WebhookDelivery
                {
                    Id = deliveryId,
                    WebhookEndpointId = endpoint.Id,
                    EventType = entry.Action,
                    Payload = WebhookPayloadFactory.BuildAuditEventPayload(deliveryId, entry),
                    Status = WebhookDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    NextAttemptAt = DateTime.UtcNow,
                });
            }

            if (deliveries.Count > 0)
                _context.WebhookDeliveries.AddRange(deliveries);
        }
    }
}
