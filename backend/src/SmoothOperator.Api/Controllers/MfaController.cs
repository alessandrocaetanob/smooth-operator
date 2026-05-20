using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.Mfa.Commands;
using SmoothOperator.Application.Features.Mfa.Queries;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/mfa")]
    [Authorize]
    public class MfaController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MfaController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _mediator.Send(new GetMfaStatusQuery(userId));
            return Ok(new { result.IsEnabled, result.RecoveryCodesRemaining });
        }

        [HttpPost("enroll")]
        public async Task<IActionResult> Enroll()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                var result = await _mediator.Send(new EnrollTotpCommand(userId));
                return Ok(new { result.SecretBase32, result.OtpAuthUri });
            }
            catch (ConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] MfaConfirmRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                var result = await _mediator.Send(new ConfirmTotpEnrollmentCommand(userId, request.Code));
                return Ok(new { recoveryCodes = result.RecoveryCodes });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Disable([FromBody] MfaDisableRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                await _mediator.Send(new DisableMfaCommand(userId, request.Password));
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        private bool TryGetUserId(out Guid userId)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out userId);
        }
    }
}
