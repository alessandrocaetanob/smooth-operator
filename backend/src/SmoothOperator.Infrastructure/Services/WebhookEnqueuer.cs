using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    public class WebhookEnqueuer : IWebhookEnqueuer
    {
        private readonly AppDbContext _context;

        public WebhookEnqueuer(AppDbContext context) => _context = context;

        public async Task EnqueueAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            // Enabled endpoints are a small set; load them and match in memory since
            // EventTypes is free-form text, not a queryable structure.
            var endpoints = await _context.WebhookEndpoints
                .AsNoTracking()
                .Where(w => w.Enabled)
                .Select(w => new { w.Id, w.EventTypes })
                .ToListAsync(cancellationToken);

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
