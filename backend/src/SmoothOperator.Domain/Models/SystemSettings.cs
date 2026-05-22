using System;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// Singleton row holding workspace-wide system settings (audit retention, etc.).
    /// </summary>
    public class SystemSettings
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Number of days to retain audit log entries before they're purged by
        /// the background retention service. 0 = keep forever.
        /// </summary>
        public int AuditLogRetentionDays { get; set; } = 0;

        /// <summary>
        /// Maximum minutes a Guacamole session may be idle (no user input) before
        /// the proxy forcibly closes the WebSocket. 0 = disabled.
        /// </summary>
        public int IdleTimeoutMinutes { get; set; } = 0;

        /// <summary>
        /// Hard cap on session lifetime measured from connection start, regardless
        /// of activity. 0 = unlimited.
        /// </summary>
        public int MaxSessionMinutes { get; set; } = 0;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
