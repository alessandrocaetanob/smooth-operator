using System;

namespace SmoothOperator.Domain.Models
{
    /// <summary>Lifecycle state of a single webhook delivery row.</summary>
    public enum WebhookDeliveryStatus
    {
        /// <summary>Awaiting its first attempt, or scheduled for a retry.</summary>
        Pending = 0,

        /// <summary>Accepted by the remote endpoint (2xx response).</summary>
        Delivered = 1,

        /// <summary>Retries exhausted; will not be attempted again.</summary>
        Dead = 2,
    }

    /// <summary>
    /// A queued (transactional-outbox) delivery of one event to one endpoint.
    /// Rows are written in the same transaction as the originating audit log and
    /// drained by the <c>WebhookDeliveryService</c> background worker.
    /// </summary>
    public class WebhookDelivery
    {
        public Guid Id { get; set; }

        public Guid WebhookEndpointId { get; set; }
        public WebhookEndpoint Endpoint { get; set; } = null!;

        /// <summary>The audit action that triggered this delivery (e.g. <c>connection.started</c>).</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>The fully rendered JSON request body.</summary>
        public string Payload { get; set; } = string.Empty;

        public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

        public int AttemptCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Earliest time the worker may attempt (or retry) this delivery.</summary>
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

        public DateTime? DeliveredAt { get; set; }

        public int? LastResponseCode { get; set; }

        /// <summary>Transport error or non-2xx detail from the most recent attempt.</summary>
        public string? LastError { get; set; }
    }
}
