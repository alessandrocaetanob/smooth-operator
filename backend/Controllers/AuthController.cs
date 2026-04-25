using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
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
        private readonly ITokenService _tokenService;

        public AuthController(AppDbContext context, IConfiguration configuration, ITokenService tokenService)
        {
            _context = context;
            _configuration = configuration;
            _tokenService = tokenService;
        }

        // Lets the frontend discover which login methods are enabled.
        [HttpGet("providers")]
        [AllowAnonymous]
        public IActionResult GetProviders()
        {
            var entraSection = _configuration.GetSection("AzureAd");
            var entraEnabled = !string.IsNullOrWhiteSpace(entraSection["ClientId"])
                               && !string.IsNullOrWhiteSpace(entraSection["TenantId"]);

            return Ok(new { Local = true, EntraId = entraEnabled });
        }

        // Local user registration. Always available – the application is the
        // source of truth for users; external IdPs are opt-in.
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Conflict(new { message = "A user with this email already exists." });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Action = "user.registered",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Details = "{\"provider\":\"local\"}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });
            await _context.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        // Local username + password login. Returns a JWT signed by this server.
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            const string invalid = "Invalid email or password.";

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                return Unauthorized(new { message = invalid });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = "user.login_failed",
                    ResourceType = "User",
                    ResourceId = user.Id.ToString(),
                    Details = "{\"provider\":\"local\"}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                });
                await _context.SaveChangesAsync();
                return Unauthorized(new { message = invalid });
            }

            if (!user.IsActive)
            {
                return StatusCode(403, new { message = "User account is disabled." });
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Action = "user.login",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Details = "{\"provider\":\"local\"}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });
            await _context.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        // Exchanges an Entra ID bearer token for an application JWT, JIT-provisioning
        // the user if needed. Only usable when Entra ID is configured.
        [HttpPost("login/entra")]
        [Authorize(AuthenticationSchemes = "EntraId")]
        public async Task<IActionResult> LoginWithEntra()
        {
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

            email = email.Trim().ToLowerInvariant();

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (objectId != null && u.EntraObjectId == objectId) || u.Email == email);

            if (user == null)
            {
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
                user.EntraObjectId = objectId;
                if (string.IsNullOrEmpty(user.Name) && !string.IsNullOrEmpty(name))
                {
                    user.Name = name;
                }
                await _context.SaveChangesAsync();
            }

            if (!user.IsActive)
            {
                return StatusCode(403, new { message = "User account is disabled." });
            }

            return Ok(BuildAuthResponse(user));
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return Ok(new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                LinkedToEntra = !string.IsNullOrEmpty(user.EntraObjectId)
            });
        }

        [HttpPost("invite")]
        [Authorize] // In a real scenario, check if the current user is an Admin
        public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Conflict("User already exists.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Email,
                IsActive = true
            };
            _context.Users.Add(user);

            var frontendUrl = _configuration["FRONTEND_URL"] ?? _configuration["APP_URL"] ?? "http://localhost:4200";
            var inviteLink = $"{frontendUrl.TrimEnd('/')}/setup-account?id={user.Id}";

            var adminEmail = User.FindFirstValue(ClaimTypes.Email)
                           ?? User.FindFirstValue("preferred_username");
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = adminUser?.Id,
                Action = "user.invited",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Details = $"{{\"email\":\"{request.Email}\"}}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });

            await _context.SaveChangesAsync();

            return Ok(new { Message = "User invited successfully.", InviteLink = inviteLink });
        }

        private AuthResponse BuildAuthResponse(User user) => new()
        {
            Token = _tokenService.CreateToken(user),
            ExpiresAt = DateTime.UtcNow.Add(_tokenService.TokenLifetime),
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                LinkedToEntra = !string.IsNullOrEmpty(user.EntraObjectId)
            }
        };
    }

    public class InviteUserRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = string.Empty;
    }
}
