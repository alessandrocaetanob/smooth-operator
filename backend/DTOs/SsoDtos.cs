using System;
using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs
{
    /// <summary>Public probe — what the login page sees. No secrets.</summary>
    public class SsoStatusDto
    {
        public bool Enabled { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>Admin view of the configured provider — includes type, name, enabled flag and (when known) sanitized config snapshot.</summary>
    public class SsoProviderDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>OIDC config without ClientSecret; null when provider is SAML.</summary>
        public OidcConfigViewDto? Oidc { get; set; }

        /// <summary>SAML config without SpPrivateKey; null when provider is OIDC.</summary>
        public SamlConfigViewDto? Saml { get; set; }
    }

    public class OidcConfigViewDto
    {
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Scopes { get; set; } = string.Empty;
        public string SubjectClaim { get; set; } = string.Empty;
        public string EmailClaim { get; set; } = string.Empty;
        public string NameClaim { get; set; } = string.Empty;
        public bool HasClientSecret { get; set; }
    }

    public class SamlConfigViewDto
    {
        public string SpEntityId { get; set; } = string.Empty;
        public string IdpEntityId { get; set; } = string.Empty;
        public string IdpSsoUrl { get; set; } = string.Empty;
        public string IdpCertificate { get; set; } = string.Empty;
        public string SpCertificate { get; set; } = string.Empty;
        public string NameIdFormat { get; set; } = string.Empty;
        public string AttributeEmail { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public bool WantAssertionsSigned { get; set; }
        public bool WantResponseSigned { get; set; }
        public bool HasSpPrivateKey { get; set; }
    }

    public class UpsertOidcRequest
    {
        [Required, StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required, Url, StringLength(2000)]
        public string Authority { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string ClientId { get; set; } = string.Empty;

        /// <summary>Send null/empty to keep the previously stored secret on edit.</summary>
        [StringLength(2000)]
        public string? ClientSecret { get; set; }

        [StringLength(500)]
        public string Scopes { get; set; } = "openid profile email";

        [StringLength(128)]
        public string SubjectClaim { get; set; } = "sub";

        [StringLength(128)]
        public string EmailClaim { get; set; } = "email";

        [StringLength(128)]
        public string NameClaim { get; set; } = "name";
    }

    public class UpsertSamlRequest
    {
        [Required, StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string SpEntityId { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string IdpEntityId { get; set; } = string.Empty;

        [Required, Url, StringLength(2000)]
        public string IdpSsoUrl { get; set; } = string.Empty;

        [Required, StringLength(20000)]
        public string IdpCertificate { get; set; } = string.Empty;

        [Required, StringLength(20000)]
        public string SpCertificate { get; set; } = string.Empty;

        /// <summary>Send null/empty to keep the previously stored private key on edit.</summary>
        [StringLength(20000)]
        public string? SpPrivateKey { get; set; }

        [StringLength(255)]
        public string NameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";

        [StringLength(128)]
        public string AttributeEmail { get; set; } = "email";

        [StringLength(128)]
        public string AttributeName { get; set; } = "name";

        public bool WantAssertionsSigned { get; set; } = true;
        public bool WantResponseSigned { get; set; } = true;
    }

    public class SetSsoEnabledRequest
    {
        public bool Enabled { get; set; }
    }
}
