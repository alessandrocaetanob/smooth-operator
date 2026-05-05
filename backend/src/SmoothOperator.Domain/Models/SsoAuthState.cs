using System;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// Short-lived state record bridging the start of an SSO authentication flow
    /// (authorization-request issued to the IdP) and its callback (code exchange or
    /// SAML assertion). One row is created per outbound request, validated on
    /// callback, then deleted (single-use, anti-replay).
    /// </summary>
    public class SsoAuthState
    {
        public Guid Id { get; set; }

        /// <summary>Random opaque value echoed back via the IdP redirect; used to bind callback to initiator (anti-CSRF).</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>OIDC nonce embedded in the authorization request and validated against the ID token claim.</summary>
        public string? Nonce { get; set; }

        /// <summary>OIDC PKCE S256 code verifier.</summary>
        public string? CodeVerifier { get; set; }

        /// <summary>Frontend path the user should be sent to after successful login (validated against same-origin allowlist).</summary>
        public string ReturnUrl { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
