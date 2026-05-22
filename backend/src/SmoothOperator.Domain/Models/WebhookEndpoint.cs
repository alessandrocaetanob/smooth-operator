using System;
using System.Collections.Generic;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// An external HTTPS endpoint registered by an administrator to receive
    /// HMAC-SHA256-signed POST requests whenever a subscribed audit event fires.
    /// </summary>
    public class WebhookEndpoint
    {
        public Guid Id { get; set; }

        /// <summary>Administrator-supplied label.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Destination URL; must be absolute HTTPS.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// AES-encrypted (via <c>IEncryptionService</c>) HMAC signing secret.
        /// The plaintext is only ever returned to the caller once, at create or
        /// rotate time.
        /// </summary>
        public string EncryptedSecret { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated event-type patterns this endpoint subscribes to.
        /// <c>*</c> matches every event; <c>user.*</c> matches by category prefix;
        /// any other value is matched exactly against the audit action string.
        /// Stored as text (not a Postgres array) so the SQLite test provider works.
        /// </summary>
        public string EventTypes { get; set; } = "*";

        public bool Enabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // --- Rollup status of the most recent delivery attempt (for the admin UI) ---

        public DateTime? LastDeliveryAt { get; set; }

        /// <summary><c>success</c> or <c>failed</c>; null until the first attempt.</summary>
        public string? LastDeliveryStatus { get; set; }

        public int? LastResponseCode { get; set; }

        /// <summary>Number of consecutive failed deliveries; reset to 0 on success.</summary>
        public int ConsecutiveFailures { get; set; }

        public ICollection<WebhookDelivery> Deliveries { get; set; } = [];
    }
}
