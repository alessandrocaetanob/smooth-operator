using System;
using System.ComponentModel.DataAnnotations;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.DTOs
{
    /// <summary>Response DTO — never includes clientSecret.</summary>
    public class SecretProviderDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsConfigured { get; set; }
        public string? VaultUri { get; set; }
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        /// <summary>True when a client secret is stored (never returns the secret value).</summary>
        public bool HasClientSecret { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateSecretProviderDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public SecretProviderType Type { get; set; }

        public bool IsEnabled { get; set; } = true;

        // Azure Key Vault fields (required when Type == AzureKeyVault)
        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        public string ClientSecret { get; set; } = string.Empty;

        [Required]
        [Url]
        public string VaultUri { get; set; } = string.Empty;
    }

    public class UpdateSecretProviderDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        // Credential fields — all optional on update; omit clientSecret to keep existing.
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }

        /// <summary>When null/empty, the existing encrypted clientSecret is preserved.</summary>
        public string? ClientSecret { get; set; }

        public string? VaultUri { get; set; }
    }
}
