using System;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class CreateApiTokenRequest
    {
        [Required]
        [MaxLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional expiry; omit or null = no expiry.</summary>
        public DateTime? ExpiresAt { get; set; }
    }

    public class ApiTokenSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    /// <summary>
    /// Returned once on creation; the plaintext token is never persisted.
    /// </summary>
    public class CreateApiTokenResult
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PlaintextToken { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
