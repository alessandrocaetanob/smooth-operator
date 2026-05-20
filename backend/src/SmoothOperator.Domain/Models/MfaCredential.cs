using System;
using System.Collections.Generic;

namespace SmoothOperator.Domain.Models
{
    public class MfaCredential
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>TOTP secret encrypted with the application Encryption__Key.</summary>
        public string EncryptedSecret { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EnabledAt { get; set; }

        public ICollection<MfaRecoveryCode> RecoveryCodes { get; set; } = [];
    }
}
