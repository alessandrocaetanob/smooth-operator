using System;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class SystemSettingsDto
    {
        public int AuditLogRetentionDays { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateSystemSettingsRequest
    {
        /// <summary>0 = keep forever. Otherwise must be between 1 and 3650 days (10 years).</summary>
        [Range(0, 3650, ErrorMessage = "Retention must be between 0 (forever) and 3650 days.")]
        public int AuditLogRetentionDays { get; set; }
    }
}
