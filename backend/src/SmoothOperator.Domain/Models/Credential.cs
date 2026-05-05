using System;

namespace SmoothOperator.Domain.Models
{
    public class Credential
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// AES-256 Encrypted password/secret
        /// </summary>
        public string EncryptedSecret { get; set; } = string.Empty;

        // Type of credential (e.g., "password", "private_key")
        public string CredentialType { get; set; } = "password";

        public string? PublicKey { get; set; }
    }
}
