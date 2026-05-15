using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Infrastructure.Services.Sso
{
    /// <summary>
    /// Injectable helper for building URLs used by the SSO flows.
    /// Base origin is sourced from AppUrls:Frontend / AppUrls:App configuration so
    /// the constructed URIs are not attacker-controllable via the Host header
    /// (guards against host-header injection when AllowedHosts is permissive).
    /// Falls back to req.Scheme://req.Host only when no base URL is configured.
    /// </summary>
    public class SsoUrlHelper
    {
        private readonly string _apiBase;
        private readonly string _frontendBase;

        public SsoUrlHelper(IOptions<AppUrlsOptions> options)
        {
            var urls = options.Value;
            // API callbacks must reach the backend directly, so prefer App URL.
            _apiBase = (!string.IsNullOrEmpty(urls.App) ? urls.App : urls.Frontend).TrimEnd('/');
            // Finalize is a frontend route, so prefer the Frontend URL.
            _frontendBase = (!string.IsNullOrEmpty(urls.Frontend) ? urls.Frontend : urls.App).TrimEnd('/');
        }

        private string ApiOrigin(HttpRequest req) =>
            string.IsNullOrEmpty(_apiBase) ? $"{req.Scheme}://{req.Host}" : _apiBase;

        private string FrontendOrigin(HttpRequest req) =>
            string.IsNullOrEmpty(_frontendBase) ? $"{req.Scheme}://{req.Host}" : _frontendBase;

        public string CallbackUrl(HttpRequest req) =>
            $"{ApiOrigin(req)}/api/auth/sso/callback";

        public string AcsUrl(HttpRequest req) =>
            $"{ApiOrigin(req)}/api/auth/sso/acs";

        public string MetadataUrl(HttpRequest req) =>
            $"{ApiOrigin(req)}/api/auth/sso/metadata";

        /// <summary>
        /// Builds the finalize redirect URL. The JWT is no longer embedded in
        /// the URL; it is delivered as an HttpOnly cookie by the caller.
        /// Only the returnUrl is passed in the fragment to avoid token exposure
        /// in browser history, reverse-proxy logs, and Referer headers.
        /// </summary>
        public string FinalizeUrl(HttpRequest req, string returnUrl)
        {
            var encodedReturn = Uri.EscapeDataString(returnUrl);
            return $"{FrontendOrigin(req)}/auth/sso/finalize#returnUrl={encodedReturn}";
        }

        public string FinalizeErrorUrl(HttpRequest req, string error)
        {
            var encoded = Uri.EscapeDataString(error);
            return $"{FrontendOrigin(req)}/auth/sso/finalize#error={encoded}";
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
