using System;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// A long-lived bearer token a user can issue from their profile for automation
    /// (CLI, CI, Terraform provider, webhook senders).
    ///
    /// Token format: <c>sop_&lt;12-char-base32-lookup&gt;_&lt;32-char-base32-secret&gt;</c>.
    /// The <see cref="TokenLookup"/> segment is stored in plaintext and indexed
    /// for O(1) lookup; <see cref="TokenHash"/> is a Base64-encoded SHA-256
    /// digest of the full plaintext token, compared with
    /// <c>CryptographicOperations.FixedTimeEquals</c>. The 32-byte (160-bit)
    /// random secret makes the token brute-force resistant on its own, so a
    /// computationally expensive password hash (BCrypt/Argon2) is unnecessary
    /// and would impose hundreds of ms of overhead on every PAT-authenticated
    /// request.
    /// </summary>
    public class ApiToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>User-supplied label (unique per user).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Public prefix used for fast lookup; e.g. <c>sop_AbCd1234EfGh</c>.</summary>
        public string TokenLookup { get; set; } = string.Empty;

        /// <summary>Base64-encoded SHA-256 digest of the full plaintext token.</summary>
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null = no expiry.</summary>
        public DateTime? ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }

        /// <summary>Set when the token is revoked; non-null = unusable.</summary>
        public DateTime? RevokedAt { get; set; }
    }
}
