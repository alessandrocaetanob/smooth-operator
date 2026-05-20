using System;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class SystemSettingsDto
    {
        public int AuditLogRetentionDays { get; set; }
        public int IdleTimeoutMinutes { get; set; }
        public int MaxSessionMinutes { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateSystemSettingsRequest
    {
        /// <summary>0 = keep forever. Otherwise must be between 1 and 3650 days (10 years).</summary>
        [Range(0, 3650, ErrorMessage = "Retention must be between 0 (forever) and 3650 days.")]
        public int AuditLogRetentionDays { get; set; }

        /// <summary>0 = disabled. Otherwise 1..10080 minutes (7 days).</summary>
        [Range(0, 10080, ErrorMessage = "Idle timeout must be between 0 (disabled) and 10080 minutes.")]
        public int IdleTimeoutMinutes { get; set; }

        /// <summary>0 = unlimited. Otherwise 1..10080 minutes (7 days).</summary>
        [Range(0, 10080, ErrorMessage = "Max session must be between 0 (unlimited) and 10080 minutes.")]
        public int MaxSessionMinutes { get; set; }
    }
}
