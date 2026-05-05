using System;

namespace SmoothOperator.Domain.Models
{
    public class Credential
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// AES-256 Encrypted password/secret. Empty when <see cref="StorageMode"/> is <see cref="SecretStorageMode.External"/>.
        /// </summary>
        public string EncryptedSecret { get; set; } = string.Empty;

        // Type of credential (e.g., "password", "private_key")
        public string CredentialType { get; set; } = "password";

        public string? PublicKey { get; set; }

        // ── External secret provider ────────────────────────────────────────
        /// <summary>Determines where the secret lives.</summary>
        public SecretStorageMode StorageMode { get; set; } = SecretStorageMode.Local;

        /// <summary>FK to the registered secret provider. Null when <see cref="StorageMode"/> is Local.</summary>
        public Guid? SecretProviderId { get; set; }

        /// <summary>Navigation property.</summary>
        public SecretProvider? SecretProvider { get; set; }

        /// <summary>Name/ID of the secret in the external vault.</summary>
        public string? ExternalSecretName { get; set; }

        /// <summary>Optional version pin; null means "latest".</summary>
        public string? ExternalSecretVersion { get; set; }
    }
}
