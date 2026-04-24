using System;

namespace Backend.Models
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

        public User? User { get; set; }
    }
}
