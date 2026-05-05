using System;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// Singleton row holding the current SMTP relay configuration.
    /// </summary>
    public class SmtpSettings
    {
        public Guid Id { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string? Username { get; set; }

        /// <summary>AES-256 encrypted SMTP password (nullable when relay accepts unauthenticated).</summary>
        public string? EncryptedPassword { get; set; }

        public string FromAddress { get; set; } = string.Empty;
        public string? FromName { get; set; }

        public bool Enabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
