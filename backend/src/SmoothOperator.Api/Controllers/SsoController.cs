using System;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Application.Features.SsoProvider.Queries;
using SmoothOperator.Infrastructure.Services.Sso;
using SmoothOperator.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/auth/sso")]
    [AllowAnonymous]
    public class SsoController : ControllerBase
    {
        private const string AuditLoginFailed = "sso.login_failed";
        private const string ResourceTypeSso = "sso";

        private readonly IMediator _mediator;
        private readonly ISsoProviderService _providers;
        private readonly IOidcFlowService _oidc;
        private readonly ISamlFlowService _saml;
        private readonly ISsoUserProvisioningService _provisioning;
        private readonly IAuditService _audit;
        private readonly ILogger<SsoController> _logger;
        private readonly SsoUrlHelper _urls;

        public SsoController(
            IMediator mediator,
            ISsoProviderService providers,
            IOidcFlowService oidc,
            ISamlFlowService saml,
            ISsoUserProvisioningService provisioning,
            IAuditService audit,
            ILogger<SsoController> logger,
            SsoUrlHelper urls)
        {
            _mediator = mediator;
            _providers = providers;
            _oidc = oidc;
            _saml = saml;
            _provisioning = provisioning;
            _audit = audit;
            _logger = logger;
            _urls = urls;
        }

        /// <summary>Public probe consumed by the login page to render the SSO button.</summary>
        [HttpGet("provider")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<SsoStatusDto>> GetProvider()
        {
            var result = await _mediator.Send(new GetSsoProviderQuery());
            return Ok(result);
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
                    SsoProviderType.Oidc => await _oidc.BuildAuthorizationUrlAsync(_urls.CallbackUrl(Request), safeReturn),
                    SsoProviderType.Saml => await _saml.BuildAuthnRequestUrlAsync(_urls.AcsUrl(Request), safeReturn),
                    _ => throw new InvalidOperationException("Unknown SSO provider type.")
                };
                return Redirect(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build SSO authorization URL");
                await _audit.WriteAsync(AuditLoginFailed, ResourceTypeSso, "",
                    new { stage = "initiate", error = ex.Message, providerType = p.Type.ToString() });
                return Redirect(_urls.FinalizeErrorUrl(Request, "initiate_failed"));
            }
        }

        /// <summary>OIDC callback. IdP redirects the browser here with code+state.</summary>
        [HttpGet("callback")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> OidcCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                await _audit.WriteAsync(AuditLoginFailed, ResourceTypeSso, "",
                    new { stage = "callback", reason = "idp_error", error });
                return Redirect(_urls.FinalizeErrorUrl(Request, error!));
            }

            try
            {
                var (identity, returnUrl) = await _oidc.HandleCallbackAsync(code ?? "", state ?? "", _urls.CallbackUrl(Request));
                var result = await _provisioning.ProvisionOrLinkAsync(SsoProviderType.Oidc, identity.ExternalId, identity.Email, identity.Name);
                await _audit.WriteAsync("sso.login_success", ResourceTypeSso, result.User.Id.ToString(),
                    new { providerType = "Oidc", email = identity.Email });
                Response.SetAuthCookie(result.Token);
                return Redirect(_urls.FinalizeUrl(Request, returnUrl));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "OIDC callback rejected");
                await _audit.WriteAsync(AuditLoginFailed, ResourceTypeSso, "",
                    new { stage = "callback", reason = ex.Message, providerType = "Oidc" });
                return Redirect(_urls.FinalizeErrorUrl(Request, "unauthorized"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OIDC callback failed");
                await _audit.WriteAsync(AuditLoginFailed, ResourceTypeSso, "",
                    new { stage = "callback", reason = "exception", error = ex.Message, providerType = "Oidc" });
                return Redirect(_urls.FinalizeErrorUrl(Request, "callback_failed"));
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
                await _audit.WriteAsync("sso.login_success", ResourceTypeSso, result.User.Id.ToString(),
                    new { providerType = "Saml", email = identity.Email });
                Response.SetAuthCookie(result.Token);
                return Redirect(_urls.FinalizeUrl(Request, returnUrl));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "SAML ACS rejected");
                await _audit.WriteAsync(AuditLoginFailed, ResourceTypeSso, "",
                    new { stage = "acs", reason = ex.Message, providerType = "Saml" });
                return Redirect(_urls.FinalizeErrorUrl(Request, "unauthorized"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SAML ACS failed");
                await _audit.WriteAsync(AuditLoginFailed, ResourceTypeSso, "",
                    new { stage = "acs", reason = "exception", error = ex.Message, providerType = "Saml" });
                return Redirect(_urls.FinalizeErrorUrl(Request, "acs_failed"));
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
                var xml = await _saml.GetSpMetadataAsync(_urls.AcsUrl(Request), _urls.MetadataUrl(Request));
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
