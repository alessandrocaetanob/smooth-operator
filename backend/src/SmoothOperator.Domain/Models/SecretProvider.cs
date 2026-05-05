using System;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// Workspace-scoped registration of an external secret manager.
    /// Multiple providers of the same or different types are supported.
    /// </summary>
    public class SecretProvider
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public SecretProviderType Type { get; set; }

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// AES-256 encrypted JSON holding provider-specific config.
        /// Shape per <see cref="Type"/>: for AzureKeyVault → { tenantId, clientId, clientSecret, vaultUri }.
        /// </summary>
        public string EncryptedConfigJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
