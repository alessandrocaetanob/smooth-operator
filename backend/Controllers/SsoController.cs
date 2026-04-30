using System;
using System.Threading.Tasks;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Services.Sso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/auth/sso")]
    [AllowAnonymous]
    public class SsoController : ControllerBase
    {
        private readonly ISsoProviderService _providers;
        private readonly IOidcFlowService _oidc;
        private readonly ISamlFlowService _saml;
        private readonly ISsoUserProvisioningService _provisioning;
        private readonly IAuditService _audit;
        private readonly ILogger<SsoController> _logger;

        public SsoController(
            ISsoProviderService providers,
            IOidcFlowService oidc,
            ISamlFlowService saml,
            ISsoUserProvisioningService provisioning,
            IAuditService audit,
            ILogger<SsoController> logger)
        {
            _providers = providers;
            _oidc = oidc;
            _saml = saml;
            _provisioning = provisioning;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>Public probe consumed by the login page to render the SSO button.</summary>
        [HttpGet("provider")]
        public async Task<ActionResult<SsoStatusDto>> GetProvider()
        {
            var p = await _providers.GetActiveProviderAsync();
            if (p == null) return Ok(new SsoStatusDto { Enabled = false });
            return Ok(new SsoStatusDto
            {
                Enabled = true,
                Type = p.Type.ToString(),
                Name = p.Name
            });
        }

        /// <summary>Begins an SSO flow. Redirects browser to the configured IdP.</summary>
        [HttpGet("initiate")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Initiate([FromQuery] string? returnUrl)
        {
            var p = await _providers.GetActiveProviderAsync();
            if (p == null) return NotFound(new { message = "SSO is not configured." });

            var safeReturn = SsoUrlHelper.SanitizeReturnUrl(returnUrl);

            try
            {
                var url = p.Type switch
                {
                    SsoProviderType.Oidc => await _oidc.BuildAuthorizationUrlAsync(SsoUrlHelper.CallbackUrl(Request), safeReturn),
                    SsoProviderType.Saml => await _saml.BuildAuthnRequestUrlAsync(SsoUrlHelper.AcsUrl(Request), safeReturn),
                    _ => throw new InvalidOperationException("Unknown SSO provider type.")
                };
                return Redirect(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build SSO authorization URL");
                await _audit.WriteAsync("sso.login_failed", "sso", "",
                    new { stage = "initiate", error = ex.Message, providerType = p.Type.ToString() });
                return Redirect(SsoUrlHelper.FinalizeErrorUrl(Request, "initiate_failed"));
            }
        }

        /// <summary>OIDC callback. IdP redirects the browser here with code+state.</summary>
        [HttpGet("callback")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> OidcCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                await _audit.WriteAsync("sso.login_failed", "sso", "",
                    new { stage = "callback", reason = "idp_error", error });
                return Redirect(SsoUrlHelper.FinalizeErrorUrl(Request, error!));
            }

            try
            {
                var (identity, returnUrl) = await _oidc.HandleCallbackAsync(code ?? "", state ?? "", SsoUrlHelper.CallbackUrl(Request));
                var result = await _provisioning.ProvisionOrLinkAsync(SsoProviderType.Oidc, identity.ExternalId, identity.Email, identity.Name);
                return Redirect(SsoUrlHelper.FinalizeUrl(Request, result.Token, returnUrl));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "OIDC callback rejected");
                await _audit.WriteAsync("sso.login_failed", "sso", "",
                    new { stage = "callback", reason = ex.Message, providerType = "Oidc" });
                return Redirect(SsoUrlHelper.FinalizeErrorUrl(Request, "unauthorized"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OIDC callback failed");
                await _audit.WriteAsync("sso.login_failed", "sso", "",
                    new { stage = "callback", reason = "exception", error = ex.Message, providerType = "Oidc" });
                return Redirect(SsoUrlHelper.FinalizeErrorUrl(Request, "callback_failed"));
            }
        }

        /// <summary>SAML Assertion Consumer Service. IdP POSTs the SAMLResponse here.</summary>
        [HttpPost("acs")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> SamlAcs()
        {
            try
            {
                var (identity, returnUrl) = await _saml.HandleAssertionAsync(Request);
                var result = await _provisioning.ProvisionOrLinkAsync(SsoProviderType.Saml, identity.ExternalId, identity.Email, identity.Name);
                return Redirect(SsoUrlHelper.FinalizeUrl(Request, result.Token, returnUrl));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "SAML ACS rejected");
                await _audit.WriteAsync("sso.login_failed", "sso", "",
                    new { stage = "acs", reason = ex.Message, providerType = "Saml" });
                return Redirect(SsoUrlHelper.FinalizeErrorUrl(Request, "unauthorized"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SAML ACS failed");
                await _audit.WriteAsync("sso.login_failed", "sso", "",
                    new { stage = "acs", reason = "exception", error = ex.Message, providerType = "Saml" });
                return Redirect(SsoUrlHelper.FinalizeErrorUrl(Request, "acs_failed"));
            }
        }

        /// <summary>SAML SP metadata XML for IdP admins to import.</summary>
        [HttpGet("metadata")]
        public async Task<IActionResult> Metadata()
        {
            var p = await _providers.GetProviderAsync();
            if (p == null || p.Type != SsoProviderType.Saml)
            {
                return NotFound(new { message = "SAML SP metadata is only available when SAML is configured." });
            }

            try
            {
                var xml = await _saml.GetSpMetadataAsync(SsoUrlHelper.AcsUrl(Request), SsoUrlHelper.MetadataUrl(Request));
                return Content(xml, "application/samlmetadata+xml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SAML metadata generation failed");
                return Problem("Failed to generate SP metadata.");
            }
        }
    }
}
