using System;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class CreateWebhookRequest
    {
        [Required]
        [MaxLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(2048, ErrorMessage = "URL must be 2048 characters or fewer.")]
        public string Url { get; set; } = string.Empty;

        /// <summary>Comma-separated event-type patterns; <c>*</c> = all events.</summary>
        [Required]
        [MaxLength(1024, ErrorMessage = "Event types must be 1024 characters or fewer.")]
        public string EventTypes { get; set; } = "*";
    }

    public class UpdateWebhookRequest
    {
        [Required]
        [MaxLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(2048, ErrorMessage = "URL must be 2048 characters or fewer.")]
        public string Url { get; set; } = string.Empty;

        [Required]
        [MaxLength(1024, ErrorMessage = "Event types must be 1024 characters or fewer.")]
        public string EventTypes { get; set; } = "*";

        public bool Enabled { get; set; } = true;
    }

    /// <summary>An endpoint as shown in the admin list — never carries the signing secret.</summary>
    public class WebhookSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string EventTypes { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastDeliveryAt { get; set; }
        public string? LastDeliveryStatus { get; set; }
        public int? LastResponseCode { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    /// <summary>
    /// Returned once on create or secret rotation; the plaintext secret is never
    /// persisted or returned again.
    /// </summary>
    public class WebhookSecretResult
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }
}
