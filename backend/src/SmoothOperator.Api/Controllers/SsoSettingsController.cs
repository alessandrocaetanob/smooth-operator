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
        private readonly IMediator _mediator;

        public SsoSettingsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [OutputCache(PolicyName = "ShortCache")]
        public async Task<ActionResult<SsoProviderDto>> Get()
        {
            var result = await _mediator.Send(new GetSsoSettingsQuery());
            return Ok(result);
        }

        [HttpPut("oidc")]
        public async Task<IActionResult> UpsertOidc([FromBody] UpsertOidcRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _mediator.Send(new UpsertOidcCommand(req));
                return NoContent();
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("saml")]
        public async Task<IActionResult> UpsertSaml([FromBody] UpsertSamlRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _mediator.Send(new UpsertSamlCommand(req));
                return NoContent();
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            try
            {
                await _mediator.Send(new DeleteSsoCommand());
                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] SetSsoEnabledRequest req)
        {
            try
            {
                await _mediator.Send(new SetSsoEnabledCommand(req.Enabled));
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
