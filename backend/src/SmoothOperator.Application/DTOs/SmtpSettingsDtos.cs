using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class SmtpSettingsDto
    {
        public bool Configured { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string? Username { get; set; }
        public string FromAddress { get; set; } = string.Empty;
        public string? FromName { get; set; }
        public bool Enabled { get; set; } = true;

        /// <summary>Indicates a stored password exists. Never returns the password itself.</summary>
        public bool HasPassword { get; set; }

        public System.DateTime? UpdatedAt { get; set; }
    }

    public class UpdateSmtpSettingsRequest
    {
        [Required, StringLength(255)]
        public string Host { get; set; } = string.Empty;

        [Range(1, 65535)]
        public int Port { get; set; } = 587;

        public bool UseSsl { get; set; } = true;

        [StringLength(255)]
        public string? Username { get; set; }

        /// <summary>When null/empty on update, the existing password is kept. Send empty string to clear.</summary>
        [StringLength(512)]
        public string? Password { get; set; }

        [Required, EmailAddress, StringLength(255)]
        public string FromAddress { get; set; } = string.Empty;

        [StringLength(255)]
        public string? FromName { get; set; }

        public bool Enabled { get; set; } = true;
    }

    public class SmtpTestRequest
    {
        [Required, EmailAddress, StringLength(255)]
        public string ToEmail { get; set; } = string.Empty;
    }
}
