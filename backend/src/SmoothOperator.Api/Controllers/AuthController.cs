using System;
using System.Security.Claims;
using System.Threading.Tasks;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.Auth.Commands;
using SmoothOperator.Application.Features.Auth.Queries;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("setup-status")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> GetSetupStatus()
        {
            var result = await _mediator.Send(new GetSetupStatusQuery());
            return Ok(new
            {
                RequiresSetup = result.RequiresSetup,
                Providers = new
                {
                    Local = result.Providers.Local,
                    Sso = result.Providers.Sso,
                    SsoType = result.Providers.SsoType,
                    SsoName = result.Providers.SsoName
                }
            });
        }

        [HttpGet("providers")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> GetProviders()
        {
            var result = await _mediator.Send(new GetProvidersQuery());
            return Ok(new { Local = result.Local, Sso = result.Sso, SsoType = result.SsoType, SsoName = result.SsoName });
        }

        [HttpPost("setup")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Setup([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _mediator.Send(new SetupCommand(request.Email, request.Name, request.Password));
                Response.SetAuthCookie(result.Token);
                // Token intentionally omitted from response body — auth is delivered via httpOnly cookie.
                return Ok(new { result.User, result.ExpiresAt });
            }
            catch (ConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _mediator.Send(new RegisterCommand(request.Email, request.Name, request.Password));
                return Ok(result);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (ConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _mediator.Send(new LoginCommand(request.Email, request.Password));
                Response.SetAuthCookie(result.Token);
                // Token intentionally omitted from response body — auth is delivered via httpOnly cookie.
                return Ok(new { result.User, result.ExpiresAt });
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var userId))
                return Unauthorized();

            try
            {
                var result = await _mediator.Send(new GetMeQuery(userId));
                return Ok(result);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("invite")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : (Guid?)null;

            try
            {
                var result = await _mediator.Send(new InviteUserCommand(
                    request.Email, request.Name, request.Role, adminId));

                return Ok(new
                {
                    Message = result.Message,
                    InviteUrl = result.InviteUrl,
                    EmailSent = result.EmailSent,
                    EmailError = result.EmailError
                });
            }
            catch (ConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("smtp-available")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> SmtpAvailable()
        {
            var result = await _mediator.Send(new SmtpAvailableQuery());
            return Ok(new { available = result.Available });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _mediator.Send(new ForgotPasswordCommand(request.Email));
            return Ok(new { Message = result.Message });
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            Response.ClearAuthCookie();
            return NoContent();
        }
    }
}
