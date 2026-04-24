using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        [Authorize]
        public async Task<IActionResult> Login()
        {
            // For Entra ID, object ID is typically in the "http://schemas.microsoft.com/identity/claims/objectidentifier" or "oid" claim
            var objectId = User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                           ?? User.FindFirstValue("oid");

            var email = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.FindFirstValue("preferred_username")
                        ?? User.FindFirstValue("upn");

            var name = User.FindFirstValue(ClaimTypes.Name)
                       ?? User.FindFirstValue("name");

            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Could not determine user email from token.");
            }

            // Check if user exists
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (objectId != null && u.EntraObjectId == objectId) || u.Email == email);

            if (user == null)
            {
                // JIT Provisioning
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Name = name ?? email,
                    EntraObjectId = objectId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);

                // Add Audit Log
                _context.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = "user.provisioned",
                    ResourceType = "User",
                    ResourceId = user.Id.ToString(),
                    Details = "{\"provider\":\"entra_id\"}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                });

                await _context.SaveChangesAsync();
            }
            else if (objectId != null && user.EntraObjectId != objectId)
            {
                // Link Entra ID if it was manually created before
                user.EntraObjectId = objectId;
                if (string.IsNullOrEmpty(user.Name) && !string.IsNullOrEmpty(name))
                {
                    user.Name = name;
                }
                await _context.SaveChangesAsync();
            }

            if (!user.IsActive)
            {
                return Forbid("User account is disabled.");
            }

            return Ok(new
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name
            });
        }

        [HttpPost("invite")]
        [Authorize] // In a real scenario, check if the current user is an Admin
        public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Email is required.");
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                return Conflict("User already exists.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim().ToLowerInvariant(),
                Name = request.Email, // Placeholder until they set up
                IsActive = true
            };

            _context.Users.Add(user);

            // In a real application, you would generate a secure, time-limited token here
            // For now, we'll just generate the link using their new User ID
            var frontendUrl = _configuration["FRONTEND_URL"] ?? _configuration["APP_URL"] ?? "http://localhost:4200";
            var inviteLink = $"{frontendUrl.TrimEnd('/')}/setup-account?id={user.Id}";

            // Add Audit Log
            var adminEmail = User.FindFirstValue(ClaimTypes.Email)
                           ?? User.FindFirstValue("preferred_username");
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = adminUser?.Id, // The admin who invited
                Action = "user.invited",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Details = $"{{\"email\":\"{request.Email}\"}}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });

            await _context.SaveChangesAsync();

            // Typically you would send an email here instead of returning the link directly,
            // but returning it for the admin to copy works for now.
            return Ok(new
            {
                Message = "User invited successfully.",
                InviteLink = inviteLink
            });
        }
    }

    public class InviteUserRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = string.Empty;
    }
}
