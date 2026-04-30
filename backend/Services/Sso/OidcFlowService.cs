using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Backend.Services.Sso
{
    public record OidcCallbackResult(string ExternalId, string Email, string? Name);

    public interface IOidcFlowService
    {
        /// <summary>
        /// Generates state, nonce, and PKCE verifier; persists an <see cref="SsoAuthState"/>;
        /// returns the IdP authorization URL the browser should be sent to.
        /// </summary>
        Task<string> BuildAuthorizationUrlAsync(string redirectUri, string returnUrl);

        /// <summary>
        /// Validates state (single-use, TTL-checked), exchanges the authorization code
        /// for tokens, validates the ID token (signature, issuer, audience, nonce, exp),
        /// and returns the extracted external identity claims plus the persisted return URL.
        /// </summary>
        Task<(OidcCallbackResult Identity, string ReturnUrl)> HandleCallbackAsync(string code, string state, string redirectUri);
    }

    public class OidcFlowService : IOidcFlowService
    {
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _db;
        private readonly ISsoProviderService _providers;
        private readonly IHttpClientFactory _httpFactory;

        public OidcFlowService(AppDbContext db, ISsoProviderService providers, IHttpClientFactory httpFactory)
        {
            _db = db;
            _providers = providers;
            _httpFactory = httpFactory;
        }

        public async Task<string> BuildAuthorizationUrlAsync(string redirectUri, string returnUrl)
        {
            var cfg = await _providers.GetDecryptedOidcAsync()
                ?? throw new InvalidOperationException("No active OIDC provider configured.");

            var disco = await GetDiscoveryAsync(cfg.Authority);

            var state = GenerateRandom(32);
            var nonce = GenerateRandom(32);
            var verifier = GenerateRandom(32);
            var challenge = CreatePkceChallenge(verifier);

            _db.SsoAuthStates.Add(new SsoAuthState
            {
                Id = Guid.NewGuid(),
                State = state,
                Nonce = nonce,
                CodeVerifier = verifier,
                ReturnUrl = returnUrl,
                ExpiresAt = DateTime.UtcNow.Add(StateTtl),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            var ru = new RequestUrl(disco.AuthorizeEndpoint
                ?? throw new InvalidOperationException("OIDC discovery returned no authorization_endpoint."));
            return ru.CreateAuthorizeUrl(
                clientId: cfg.ClientId,
                responseType: "code",
                scope: cfg.Scopes,
                redirectUri: redirectUri,
                state: state,
                nonce: nonce,
                codeChallenge: challenge,
                codeChallengeMethod: "S256");
        }

        public async Task<(OidcCallbackResult Identity, string ReturnUrl)> HandleCallbackAsync(string code, string state, string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code is required");
            if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state is required");

            var stateRow = await _db.SsoAuthStates.FirstOrDefaultAsync(s => s.State == state)
                ?? throw new UnauthorizedAccessException("Invalid SSO state.");

            // Single-use: always remove, regardless of subsequent validation outcome.
            _db.SsoAuthStates.Remove(stateRow);
            await _db.SaveChangesAsync();

            if (stateRow.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Expired SSO state.");

            var cfg = await _providers.GetDecryptedOidcAsync()
                ?? throw new InvalidOperationException("OIDC provider configuration disappeared mid-flow.");

            var disco = await GetDiscoveryAsync(cfg.Authority);

            var http = _httpFactory.CreateClient("sso-oidc");
            var tokenResponse = await http.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = cfg.ClientId,
                ClientSecret = cfg.ClientSecret,
                Code = code,
                RedirectUri = redirectUri,
                CodeVerifier = stateRow.CodeVerifier
            });

            if (tokenResponse.IsError)
                throw new UnauthorizedAccessException($"OIDC token exchange failed: {tokenResponse.Error}");

            if (string.IsNullOrWhiteSpace(tokenResponse.IdentityToken))
                throw new UnauthorizedAccessException("OIDC token response missing id_token.");

            var principal = await ValidateIdTokenAsync(tokenResponse.IdentityToken, cfg, disco, stateRow.Nonce);

            string? GetClaim(string type) => principal.FindFirst(type)?.Value;

            var externalId = GetClaim(cfg.SubjectClaim) ?? GetClaim("sub")
                ?? throw new UnauthorizedAccessException("OIDC ID token missing subject claim.");
            var email = GetClaim(cfg.EmailClaim) ?? GetClaim("email")
                ?? throw new UnauthorizedAccessException("OIDC ID token missing email claim.");
            var name = GetClaim(cfg.NameClaim) ?? GetClaim("name");

            return (new OidcCallbackResult(externalId, email, name), stateRow.ReturnUrl);
        }

        private async Task<DiscoveryDocumentResponse> GetDiscoveryAsync(string authority)
        {
            var http = _httpFactory.CreateClient("sso-oidc");
            var disco = await http.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = authority,
                Policy = new DiscoveryPolicy { ValidateEndpoints = false }
            });
            if (disco.IsError) throw new InvalidOperationException($"OIDC discovery failed: {disco.Error}");
            return disco;
        }

        private static async Task<System.Security.Claims.ClaimsPrincipal> ValidateIdTokenAsync(
            string idToken, OidcConfig cfg, DiscoveryDocumentResponse disco, string? expectedNonce)
        {
            // Use disco.Issuer (canonical clean URL) so we never double-append /.well-known/openid-configuration
            // regardless of what the user typed in the authority field.
            var metadataAddress = $"{disco.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var oidcConfig = await configManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = disco.Issuer,
                ValidateAudience = true,
                ValidAudience = cfg.ClientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(idToken, validationParameters, out var validated);

            if (!string.IsNullOrEmpty(expectedNonce))
            {
                var nonceClaim = principal.FindFirst("nonce")?.Value;
                if (!string.Equals(nonceClaim, expectedNonce, StringComparison.Ordinal))
                    throw new SecurityTokenValidationException("OIDC nonce mismatch.");
            }

            return principal;
        }

        private static string GenerateRandom(int byteLength)
        {
            var buffer = new byte[byteLength];
            RandomNumberGenerator.Fill(buffer);
            return Base64UrlEncode(buffer);
        }

        private static string CreatePkceChallenge(string verifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
