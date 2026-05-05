using System;

namespace SmoothOperator.Domain.Models
{
    public enum SsoProviderType
    {
        Oidc = 0,
        Saml = 1
    }

    /// <summary>
    /// Logically singleton row holding the workspace's single configured SSO provider
    /// (either OIDC or SAML 2.0). The UI enforces a single row; admins swap protocols
    /// by editing rather than maintaining multiple providers.
    /// </summary>
    public class SsoProvider
    {
        public Guid Id { get; set; }

        /// <summary>Display label rendered on the login button.</summary>
        public string Name { get; set; } = string.Empty;

        public SsoProviderType Type { get; set; }

        public bool IsEnabled { get; set; }

        /// <summary>
        /// AES-256 encrypted JSON payload holding the provider configuration.
        /// Shape depends on <see cref="Type"/> (OidcConfig or SamlConfig).
        /// </summary>
        public string EncryptedConfigJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
