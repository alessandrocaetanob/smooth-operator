using System;

namespace SmoothOperator.Domain.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid? UserId { get; set; }
        public string Action { get; set; } = string.Empty; // e.g., "connection.started", "user.created"
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty; // JSON metadata
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public string? CorrelationId { get; set; }
        /// <summary>Outcome of the audited operation: "success" or "failure".</summary>
        public string Outcome { get; set; } = "success";

        public User? User { get; set; }
    }
}
