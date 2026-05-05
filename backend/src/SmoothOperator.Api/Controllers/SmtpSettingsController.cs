using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.SmtpSettings.Commands;
using SmoothOperator.Application.Features.SmtpSettings.Queries;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/settings/smtp")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SmtpSettingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SmtpSettingsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<SmtpSettingsDto>> Get()
        {
            var result = await _mediator.Send(new GetSmtpSettingsQuery());
            return Ok(result);
        }

        [HttpPut]
        public async Task<ActionResult<SmtpSettingsDto>> Update([FromBody] UpdateSmtpSettingsRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _mediator.Send(new SaveSmtpSettingsCommand(request));
            return Ok(result);
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test([FromBody] SmtpTestRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _mediator.Send(new SendSmtpTestCommand(request.ToEmail));
            if (!result.Success)
            {
                if (result.Error == "SMTP relay is not configured.")
                    return BadRequest(new { message = result.Error });
                return StatusCode(502, new { message = result.Error ?? "SMTP send failed." });
            }
            return Ok(new { success = true });
        }
    }
}
