using System;

namespace SmoothOperator.Domain.Models
{
    public class Invitation
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User? User { get; set; }

        /// <summary>SHA-256 hash of the raw token (raw token is only shown once).</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>Type of invite: "user_invite" or "password_reset".</summary>
        public string Type { get; set; } = "user_invite";

        public DateTime ExpiresAt { get; set; }
        public DateTime? RedeemedAt { get; set; }

        public Guid? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
