using System;
using System.Threading.Tasks;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Services.Sso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/settings/sso")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SsoSettingsController : ControllerBase
    {
        private readonly ISsoProviderService _providers;
        private readonly IAuditService _audit;
        private readonly ISsoConnectionTester _tester;

        public SsoSettingsController(ISsoProviderService providers, IAuditService audit, ISsoConnectionTester tester)
        {
            _providers = providers;
            _audit = audit;
            _tester = tester;
        }

        [HttpGet]
        public async Task<ActionResult<SsoProviderDto>> Get()
        {
            var p = await _providers.GetProviderAsync();
            if (p == null) return Ok(new SsoProviderDto { Type = string.Empty });

            var dto = new SsoProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type.ToString(),
                IsEnabled = p.IsEnabled,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };

            if (p.Type == SsoProviderType.Oidc)
            {
                var c = await _providers.GetDecryptedOidcAsync();
                if (c != null)
                {
                    dto.Oidc = new OidcConfigViewDto
                    {
                        Authority = c.Authority,
                        ClientId = c.ClientId,
                        Scopes = c.Scopes,
                        SubjectClaim = c.SubjectClaim,
                        EmailClaim = c.EmailClaim,
                        NameClaim = c.NameClaim,
                        HasClientSecret = !string.IsNullOrEmpty(c.ClientSecret)
                    };
                }
            }
            else if (p.Type == SsoProviderType.Saml)
            {
                var c = await _providers.GetDecryptedSamlAsync();
                if (c != null)
                {
                    dto.Saml = new SamlConfigViewDto
                    {
                        SpEntityId = c.SpEntityId,
                        IdpEntityId = c.IdpEntityId,
                        IdpSsoUrl = c.IdpSsoUrl,
                        IdpCertificate = c.IdpCertificate,
                        SpCertificate = c.SpCertificate,
                        NameIdFormat = c.NameIdFormat,
                        AttributeEmail = c.AttributeEmail,
                        AttributeName = c.AttributeName,
                        WantAssertionsSigned = c.WantAssertionsSigned,
                        WantResponseSigned = c.WantResponseSigned,
                        HasSpPrivateKey = !string.IsNullOrEmpty(c.SpPrivateKey)
                    };
                }
            }
            return Ok(dto);
        }

        [HttpPut("oidc")]
        public async Task<IActionResult> UpsertOidc([FromBody] UpsertOidcRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _providers.GetDecryptedOidcAsync();
            var clientSecret = string.IsNullOrWhiteSpace(req.ClientSecret)
                ? existing?.ClientSecret ?? string.Empty
                : req.ClientSecret;

            if (string.IsNullOrEmpty(clientSecret))
            {
                ModelState.AddModelError(nameof(req.ClientSecret), "ClientSecret is required when configuring OIDC for the first time.");
                return BadRequest(ModelState);
            }

            var cfg = new OidcConfig
            {
                Authority = req.Authority.Trim()
                    .TrimEnd('/')
                    .Replace("/.well-known/openid-configuration", string.Empty, StringComparison.OrdinalIgnoreCase),
                ClientId = req.ClientId.Trim(),
                ClientSecret = clientSecret,
                Scopes = string.IsNullOrWhiteSpace(req.Scopes) ? "openid profile email" : req.Scopes.Trim(),
                SubjectClaim = req.SubjectClaim.Trim(),
                EmailClaim = req.EmailClaim.Trim(),
                NameClaim = req.NameClaim.Trim()
            };

            var saved = await _providers.UpsertOidcAsync(req.Name.Trim(), cfg);
            await _audit.WriteAsync("sso.config_updated", "sso", saved.Id.ToString(),
                new { type = "Oidc", saved.Name, authority = cfg.Authority });
            return NoContent();
        }

        [HttpPut("saml")]
        public async Task<IActionResult> UpsertSaml([FromBody] UpsertSamlRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _providers.GetDecryptedSamlAsync();
            var spPrivateKey = string.IsNullOrWhiteSpace(req.SpPrivateKey)
                ? existing?.SpPrivateKey ?? string.Empty
                : req.SpPrivateKey;

            if (string.IsNullOrEmpty(spPrivateKey))
            {
                ModelState.AddModelError(nameof(req.SpPrivateKey), "SpPrivateKey is required when configuring SAML for the first time.");
                return BadRequest(ModelState);
            }

            var cfg = new SamlConfig
            {
                SpEntityId = req.SpEntityId.Trim(),
                IdpEntityId = req.IdpEntityId.Trim(),
                IdpSsoUrl = req.IdpSsoUrl.Trim(),
                IdpCertificate = req.IdpCertificate.Trim(),
                SpCertificate = req.SpCertificate.Trim(),
                SpPrivateKey = spPrivateKey,
                NameIdFormat = req.NameIdFormat.Trim(),
                AttributeEmail = req.AttributeEmail.Trim(),
                AttributeName = req.AttributeName.Trim(),
                WantAssertionsSigned = req.WantAssertionsSigned,
                WantResponseSigned = req.WantResponseSigned
            };

            var saved = await _providers.UpsertSamlAsync(req.Name.Trim(), cfg);
            await _audit.WriteAsync("sso.config_updated", "sso", saved.Id.ToString(),
                new { type = "Saml", saved.Name, idpEntityId = cfg.IdpEntityId });
            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            var existing = await _providers.GetProviderAsync();
            var ok = await _providers.DeleteAsync();
            if (!ok) return NotFound();
            await _audit.WriteAsync("sso.config_deleted", "sso", existing?.Id.ToString() ?? "",
                new { type = existing?.Type.ToString() });
            return NoContent();
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] SetSsoEnabledRequest req)
        {
            var p = await _providers.SetEnabledAsync(req.Enabled);
            if (p == null) return NotFound(new { message = "Configure an SSO provider before enabling it." });
            await _audit.WriteAsync(req.Enabled ? "sso.enabled" : "sso.disabled", "sso", p.Id.ToString(),
                new { type = p.Type.ToString() });
            return NoContent();
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test()
        {
            var result = await _tester.TestAsync();
            await _audit.WriteAsync("sso.tested", "sso", "",
                new { success = result.Success, message = result.Message });
            if (!result.Success) return BadRequest(new { message = result.Message, details = result.Details });
            return Ok(new { success = true, message = result.Message, details = result.Details });
        }
    }
}
