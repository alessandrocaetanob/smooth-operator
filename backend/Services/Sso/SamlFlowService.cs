using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Sso
{
    public record SamlAssertionResult(string ExternalId, string Email, string? Name);

    public interface ISamlFlowService
    {
        /// <summary>
        /// Builds a SAML AuthnRequest, persists an <see cref="SsoAuthState"/> keyed by RelayState,
        /// and returns the redirect URL the browser should be sent to (HTTP-Redirect binding).
        /// </summary>
        Task<string> BuildAuthnRequestUrlAsync(string acsUrl, string returnUrl);

        /// <summary>
        /// Parses and validates a SAML Response received at the ACS endpoint:
        /// signature, conditions (audience, NotBefore, NotOnOrAfter), and RelayState single-use.
        /// Returns the extracted NameID + attributes plus the persisted return URL.
        /// </summary>
        Task<(SamlAssertionResult Identity, string ReturnUrl)> HandleAssertionAsync(HttpRequest request);

        /// <summary>Returns the SP metadata XML for IdP admins to consume.</summary>
        Task<string> GetSpMetadataAsync(string acsUrl, string spMetadataUrl);
    }

    public class SamlFlowService : ISamlFlowService
    {
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _db;
        private readonly ISsoProviderService _providers;

        public SamlFlowService(AppDbContext db, ISsoProviderService providers)
        {
            _db = db;
            _providers = providers;
        }

        public async Task<string> BuildAuthnRequestUrlAsync(string acsUrl, string returnUrl)
        {
            var cfg = await RequireSamlConfigAsync();
            var samlConfig = BuildSamlConfiguration(cfg, acsUrl);

            var relayState = Guid.NewGuid().ToString("N");

            _db.SsoAuthStates.Add(new SsoAuthState
            {
                Id = Guid.NewGuid(),
                State = relayState,
                ReturnUrl = returnUrl,
                ExpiresAt = DateTime.UtcNow.Add(StateTtl),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            var authn = new Saml2AuthnRequest(samlConfig)
            {
                AssertionConsumerServiceUrl = new Uri(acsUrl),
                Destination = new Uri(cfg.IdpSsoUrl),
                NameIdPolicy = new NameIdPolicy
                {
                    AllowCreate = true,
                    Format = cfg.NameIdFormat
                }
            };

            var binding = new Saml2RedirectBinding { RelayState = relayState };
            binding.Bind(authn);
            return binding.RedirectLocation.ToString();
        }

        public async Task<(SamlAssertionResult Identity, string ReturnUrl)> HandleAssertionAsync(HttpRequest request)
        {
            var cfg = await RequireSamlConfigAsync();
            // ACS URL must match what we sent; rebuild from request.
            var acsUrl = $"{request.Scheme}://{request.Host}{request.Path}";
            var samlConfig = BuildSamlConfiguration(cfg, acsUrl);

            var binding = new Saml2PostBinding();
            var response = new Saml2AuthnResponse(samlConfig);

            binding.ReadSamlResponse(GenericHttpRequest(request), response);

            if (response.Status != Saml2StatusCodes.Success)
                throw new UnauthorizedAccessException($"SAML response status: {response.Status}");

            // Unpack first; this validates signature + decrypts + checks conditions per samlConfig.
            binding.Unbind(GenericHttpRequest(request), response);

            var relayState = binding.RelayState;
            if (string.IsNullOrWhiteSpace(relayState))
                throw new UnauthorizedAccessException("SAML response missing RelayState.");

            var stateRow = await _db.SsoAuthStates.FirstOrDefaultAsync(s => s.State == relayState)
                ?? throw new UnauthorizedAccessException("Invalid SAML RelayState.");

            _db.SsoAuthStates.Remove(stateRow);
            await _db.SaveChangesAsync();

            if (stateRow.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Expired SAML RelayState.");

            var nameId = response.NameId?.Value ?? throw new UnauthorizedAccessException("SAML assertion missing NameID.");
            var claims = response.ClaimsIdentity?.Claims.ToList() ?? new System.Collections.Generic.List<System.Security.Claims.Claim>();

            string? Find(string type) => claims.FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;

            var email = Find(cfg.AttributeEmail) ?? Find("email") ?? Find(System.Security.Claims.ClaimTypes.Email)
                ?? nameId;
            var name = Find(cfg.AttributeName) ?? Find("name") ?? Find(System.Security.Claims.ClaimTypes.Name);

            return (new SamlAssertionResult(nameId, email, name), stateRow.ReturnUrl);
        }

        public async Task<string> GetSpMetadataAsync(string acsUrl, string spMetadataUrl)
        {
            var cfg = await RequireSamlConfigAsync();
            var samlConfig = BuildSamlConfiguration(cfg, acsUrl);

            var entityDescriptor = new EntityDescriptor(samlConfig)
            {
                ValidUntil = 365,
                SPSsoDescriptor = new SPSsoDescriptor
                {
                    AuthnRequestsSigned = true,
                    WantAssertionsSigned = cfg.WantAssertionsSigned,
                    SigningCertificates = new[] { samlConfig.SigningCertificate },
                    NameIDFormats = new[] { new Uri(cfg.NameIdFormat) },
                    AssertionConsumerServices = new[]
                    {
                        new AssertionConsumerService { Binding = ProtocolBindings.HttpPost, Location = new Uri(acsUrl) }
                    }
                }
            };
            return new Saml2Metadata(entityDescriptor).CreateMetadata().ToXml();
        }

        private async Task<SamlConfig> RequireSamlConfigAsync() =>
            await _providers.GetDecryptedSamlAsync()
                ?? throw new InvalidOperationException("No active SAML provider configured.");

        private static Saml2Configuration BuildSamlConfiguration(SamlConfig cfg, string acsUrl)
        {
            var spCert = LoadCertificateWithKey(cfg.SpCertificate, cfg.SpPrivateKey);
            var idpCert = LoadCertificate(cfg.IdpCertificate);

            var saml = new Saml2Configuration
            {
                Issuer = cfg.SpEntityId,
                SignatureAlgorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
                SigningCertificate = spCert,
                AllowedIssuer = cfg.IdpEntityId,
                SingleSignOnDestination = new Uri(cfg.IdpSsoUrl)
            };
            saml.SignAuthnRequest = true;
            saml.AllowedAudienceUris.Add(cfg.SpEntityId);
            saml.SignatureValidationCertificates.Add(idpCert);
            return saml;
        }

        private static X509Certificate2 LoadCertificate(string pem)
        {
            var bytes = TryParsePem(pem) ?? Convert.FromBase64String(pem);
            return X509CertificateLoader.LoadCertificate(bytes);
        }

        private static X509Certificate2 LoadCertificateWithKey(string certPem, string keyPem)
        {
            using var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
            // CreateFromPem returns an ephemeral key; persist for use by signing routines.
            return X509CertificateLoader.LoadPkcs12(
                cert.Export(X509ContentType.Pkcs12),
                password: null,
                keyStorageFlags: X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }

        private static byte[]? TryParsePem(string input)
        {
            try
            {
                var trimmed = input.Trim();
                if (!trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal)) return null;
                var b64 = string.Concat(trimmed
                    .Split('\n')
                    .Where(l => !l.StartsWith("-----", StringComparison.Ordinal))
                    .Select(l => l.Trim()));
                return Convert.FromBase64String(b64);
            }
            catch
            {
                return null;
            }
        }

        private static ITfoxtec.Identity.Saml2.Http.HttpRequest GenericHttpRequest(HttpRequest request)
        {
            var generic = new ITfoxtec.Identity.Saml2.Http.HttpRequest
            {
                Method = request.Method,
                QueryString = request.QueryString.Value ?? string.Empty,
                Query = System.Web.HttpUtility.ParseQueryString(request.QueryString.Value ?? string.Empty)
            };
            if (request.HasFormContentType)
            {
                var form = new System.Collections.Specialized.NameValueCollection();
                foreach (var kv in request.Form)
                {
                    form.Add(kv.Key, kv.Value.ToString());
                }
                generic.Form = form;
            }
            return generic;
        }
    }
}
