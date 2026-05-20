using System;

namespace SmoothOperator.Domain.Models
{
    public class MfaRecoveryCode
    {
        public Guid Id { get; set; }
        public Guid MfaCredentialId { get; set; }
        public MfaCredential MfaCredential { get; set; } = null!;

        /// <summary>BCrypt hash of the plaintext recovery code.</summary>
        public string CodeHash { get; set; } = string.Empty;

        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
    }
}
