using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.Invites.Commands;
using SmoothOperator.Application.Features.Invites.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/invites")]
    [AllowAnonymous]
    public class InvitesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InvitesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("{token}")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Preview(string token)
        {
            try
            {
                var result = await _mediator.Send(new ValidateInviteQuery(token));
                return Ok(result);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{token}/redeem")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Redeem(string token, [FromBody] RedeemInviteRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _mediator.Send(new RedeemInviteCommand(token, request.Password, request.Name));
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
