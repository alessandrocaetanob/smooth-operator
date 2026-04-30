using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Backend.Models;
using Duende.IdentityModel.Client;

namespace Backend.Services.Sso
{
    public record SsoTestResult(bool Success, string Message, object? Details = null);

    public interface ISsoConnectionTester
    {
        Task<SsoTestResult> TestAsync();
    }

    /// <summary>
    /// Lightweight probe of the currently saved SSO provider. For OIDC, fetches the
    /// discovery document and surfaces issuer + endpoints. For SAML, validates the
    /// stored IdP certificate parses and that required URLs are well-formed.
    /// Performs no authentication — intended as an admin sanity check before enabling.
    /// </summary>
    public class SsoConnectionTester : ISsoConnectionTester
    {
        private readonly ISsoProviderService _providers;
        private readonly IHttpClientFactory _httpFactory;

        public SsoConnectionTester(ISsoProviderService providers, IHttpClientFactory httpFactory)
        {
            _providers = providers;
            _httpFactory = httpFactory;
        }

        public async Task<SsoTestResult> TestAsync()
        {
            var p = await _providers.GetProviderAsync();
            if (p == null) return new SsoTestResult(false, "No SSO provider configured.");

            try
            {
                if (p.Type == SsoProviderType.Oidc)
                {
                    var cfg = await _providers.GetDecryptedOidcAsync()
                        ?? throw new InvalidOperationException("OIDC config could not be decrypted.");
                    var http = _httpFactory.CreateClient("sso-oidc");
                    var disco = await http.GetDiscoveryDocumentAsync(cfg.Authority);
                    if (disco.IsError)
                        return new SsoTestResult(false, $"OIDC discovery failed: {disco.Error}");
                    return new SsoTestResult(true, "OIDC discovery succeeded.", new
                    {
                        issuer = disco.Issuer,
                        authorizeEndpoint = disco.AuthorizeEndpoint,
                        tokenEndpoint = disco.TokenEndpoint,
                        userInfoEndpoint = disco.UserInfoEndpoint
                    });
                }

                if (p.Type == SsoProviderType.Saml)
                {
                    var cfg = await _providers.GetDecryptedSamlAsync()
                        ?? throw new InvalidOperationException("SAML config could not be decrypted.");
                    if (!Uri.TryCreate(cfg.IdpSsoUrl, UriKind.Absolute, out _))
                        return new SsoTestResult(false, "IdpSsoUrl is not a valid absolute URL.");

                    try
                    {
                        var pem = cfg.IdpCertificate?.Trim() ?? string.Empty;
                        var cert = X509Certificate2.CreateFromPem(pem);
                        return new SsoTestResult(true, "SAML configuration parsed successfully.", new
                        {
                            idpEntityId = cfg.IdpEntityId,
                            idpSsoUrl = cfg.IdpSsoUrl,
                            certificateSubject = cert.Subject,
                            certificateNotAfter = cert.NotAfter
                        });
                    }
                    catch (Exception ex)
                    {
                        return new SsoTestResult(false, $"IdpCertificate could not be parsed: {ex.Message}");
                    }
                }

                return new SsoTestResult(false, $"Unknown provider type: {p.Type}.");
            }
            catch (Exception ex)
            {
                return new SsoTestResult(false, ex.Message);
            }
        }
    }
}
