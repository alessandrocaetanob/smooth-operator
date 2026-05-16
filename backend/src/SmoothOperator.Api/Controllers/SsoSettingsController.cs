using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.SsoSettings.Commands;
using SmoothOperator.Application.Features.SsoSettings.Queries;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/settings/sso")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SsoSettingsController : ControllerBase
    {
        private const string CacheTag = "sso-settings";
        private readonly IMediator _mediator;
        private readonly IOutputCacheStore _cacheStore;

        public SsoSettingsController(IMediator mediator, IOutputCacheStore cacheStore)
        {
            _mediator = mediator;
            _cacheStore = cacheStore;
        }

        [HttpGet]
        [OutputCache(PolicyName = "ShortCache", Tags = [CacheTag])]
        public async Task<ActionResult<SsoProviderDto>> Get()
        {
            var result = await _mediator.Send(new GetSsoSettingsQuery());
            return Ok(result);
        }

        [HttpPut("oidc")]
        public async Task<IActionResult> UpsertOidc(
            [FromBody] UpsertOidcRequest req,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _mediator.Send(new UpsertOidcCommand(req), cancellationToken);
                await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
                return NoContent();
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("saml")]
        public async Task<IActionResult> UpsertSaml(
            [FromBody] UpsertSamlRequest req,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _mediator.Send(new UpsertSamlCommand(req), cancellationToken);
                await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
                return NoContent();
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(CancellationToken cancellationToken)
        {
            try
            {
                await _mediator.Send(new DeleteSsoCommand(), cancellationToken);
                await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle(
            [FromBody] SetSsoEnabledRequest req,
            CancellationToken cancellationToken)
        {
            try
            {
                await _mediator.Send(new SetSsoEnabledCommand(req.Enabled), cancellationToken);
                await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test()
        {
            var result = await _mediator.Send(new TestSsoConnectionCommand());
            if (!result.Success)
                return BadRequest(new { message = result.Message, details = result.Details });
            return Ok(new { success = true, message = result.Message, details = result.Details });
        }
    }
}
