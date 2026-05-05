using System;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/invites")]
    [AllowAnonymous]
    public class InvitesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IInviteService _invites;
        private readonly IAuditService _audit;

        public InvitesController(AppDbContext context, IInviteService invites, IAuditService audit)
        {
            _context = context;
            _invites = invites;
            _audit = audit;
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> Preview(string token)
        {
            var invitation = await _invites.ValidateAsync(token);
            if (invitation == null || invitation.User == null)
            {
                return NotFound(new { message = "Invitation is invalid or has expired." });
            }

            return Ok(new InvitePreviewDto
            {
                Email = invitation.User.Email,
                Name = invitation.User.Name,
                Type = invitation.Type,
            });
        }

        [HttpPost("{token}/redeem")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Redeem(string token, [FromBody] RedeemInviteRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var invitation = await _invites.ValidateAsync(token);
            if (invitation == null || invitation.User == null)
            {
                return NotFound(new { message = "Invitation is invalid or has expired." });
            }

            var user = invitation.User;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                user.Name = request.Name.Trim();
            }
            user.IsActive = true;

            await _invites.RedeemAsync(token);

            await _audit.WriteAsync("invite.redeemed", "User", user.Id.ToString(),
                new { type = invitation.Type, userId = user.Id });

            return NoContent();
        }
    }
}
