using System;
using Microsoft.AspNetCore.Http;

namespace Backend.Services.Sso
{
    /// <summary>
    /// Helpers for safely building same-origin URLs used by the SSO flows.
    /// Trust X-Forwarded-* headers because Program.cs configures
    /// <see cref="Microsoft.AspNetCore.Builder.ForwardedHeadersExtensions.UseForwardedHeaders"/>.
    /// </summary>
    public static class SsoUrlHelper
    {
        public static string Origin(HttpRequest req) =>
            $"{req.Scheme}://{req.Host}";

        public static string CallbackUrl(HttpRequest req) =>
            $"{Origin(req)}/api/auth/sso/callback";

        public static string AcsUrl(HttpRequest req) =>
            $"{Origin(req)}/api/auth/sso/acs";

        public static string MetadataUrl(HttpRequest req) =>
            $"{Origin(req)}/api/auth/sso/metadata";

        public static string FinalizeUrl(HttpRequest req, string token, string returnUrl)
        {
            var encodedToken = Uri.EscapeDataString(token);
            var encodedReturn = Uri.EscapeDataString(returnUrl);
            return $"{Origin(req)}/auth/sso/finalize?token={encodedToken}&returnUrl={encodedReturn}";
        }

        public static string FinalizeErrorUrl(HttpRequest req, string error)
        {
            var encoded = Uri.EscapeDataString(error);
            return $"{Origin(req)}/auth/sso/finalize?error={encoded}";
        }

        /// <summary>
        /// Same-origin path-only allowlist. Returns the sanitized return URL or "/" when
        /// the input is missing, absolute, protocol-relative, contains backslashes, or
        /// otherwise unsafe. Prevents open-redirect through the SSO flow.
        /// </summary>
        public static string SanitizeReturnUrl(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "/";
            var trimmed = input.Trim();
            if (trimmed.Length > 1024) return "/";
            if (!trimmed.StartsWith('/')) return "/";
            if (trimmed.StartsWith("//")) return "/";
            if (trimmed.Contains('\\')) return "/";
            if (trimmed.Contains(':')) return "/";
            return trimmed;
        }
    }
}
