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
        private readonly IInviteService _inviteService;
        private readonly IEmailService _emailService;

        public AuthController(
            AppDbContext context,
            IConfiguration configuration,
            ITokenService tokenService,
            IInviteService inviteService,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _tokenService = tokenService;
            _inviteService = inviteService;
            _emailService = emailService;
        }

        // Lets the frontend discover which login methods are enabled and whether
        // the application still needs first-time setup (no users yet).
        [HttpGet("setup-status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSetupStatus()
        {
            var hasUsers = await _context.Users.AnyAsync();
            return Ok(new
            {
                RequiresSetup = !hasUsers,
                Providers = new
                {
                    Local = true,
                    EntraId = IsEntraEnabled()
                }
            });
        }

        // Backwards-compatible alias used by older clients.
        [HttpGet("providers")]
        [AllowAnonymous]
        public IActionResult GetProviders()
            => Ok(new { Local = true, EntraId = IsEntraEnabled() });

        private bool IsEntraEnabled()
        {
            var entraSection = _configuration.GetSection("AzureAd");
            return !string.IsNullOrWhiteSpace(entraSection["ClientId"])
                && !string.IsNullOrWhiteSpace(entraSection["TenantId"]);
        }

        // First-time bootstrap: creates the very first (root/owner) user.
        // Only succeeds while the Users table is empty so this endpoint can never
        // be used for self-signup once the application has been initialised.
        [HttpPost("setup")]
        [AllowAnonymous]
        public async Task<IActionResult> Setup([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (await _context.Users.AnyAsync())
            {
                return Conflict(new { message = "Setup has already been completed." });
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Ensure an Owner role exists and link it to the bootstrap user.
            var ownerRole = await RequireRoleAsync(
                AppRoles.Owner,
                "Root access. First account created during setup.");
            user.Roles.Add(ownerRole);

            _context.Users.Add(user);
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Action = "user.bootstrap",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Details = "{\"provider\":\"local\",\"role\":\"Owner\"}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });
            await _context.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        // Local user registration. Restricted to authenticated callers – the
        // application is the source of truth for users and additional accounts
        // are created via invite or by an existing administrator.
        [HttpPost("register")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Conflict(new { message = "A user with this email already exists." });
            }

            var defaultUserRole = await RequireRoleAsync(
                AppRoles.User,
                "Can use connections in assigned vaults.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.Roles.Add(defaultUserRole);

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
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == email);

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

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u =>
                (objectId != null && u.EntraObjectId == objectId) || u.Email == email);

            if (user == null)
            {
                var defaultUserRole = await RequireRoleAsync(
                    AppRoles.User,
                    "Can use connections in assigned vaults.");

                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Name = name ?? email,
                    EntraObjectId = objectId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                user.Roles.Add(defaultUserRole);
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

            if (user.Roles.Count == 0)
            {
                var defaultUserRole = await RequireRoleAsync(
                    AppRoles.User,
                    "Can use connections in assigned vaults.");
                user.Roles.Add(defaultUserRole);
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

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            return Ok(new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                LinkedToEntra = !string.IsNullOrEmpty(user.EntraObjectId),
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            });
        }

        [HttpPost("invite")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Conflict("User already exists.");
            }

            var defaultUserRole = await RequireRoleAsync(
                AppRoles.User,
                "Can use connections in assigned vaults.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Email,
                IsActive = false
            };
            user.Roles.Add(defaultUserRole);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var adminEmail = User.FindFirstValue(ClaimTypes.Email)
                           ?? User.FindFirstValue("preferred_username");
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

            var (_, _, inviteUrl) = await _inviteService.CreateAsync(
                user.Id, InviteService.TypeUserInvite, TimeSpan.FromHours(72), adminUser?.Id);

            var emailSent = false;
            string? emailError = null;
            if (await _emailService.IsConfiguredAsync())
            {
                var result = await _emailService.SendInviteAsync(user.Email, user.Name, inviteUrl);
                emailSent = result.Success;
                emailError = result.Error;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = adminUser?.Id,
                Action = "user.invited",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Details = System.Text.Json.JsonSerializer.Serialize(new { email = user.Email, emailSent }),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "User invited successfully.",
                InviteUrl = inviteUrl,
                EmailSent = emailSent,
                EmailError = emailError
            });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Always succeed to avoid email enumeration. Only attempt send when SMTP is configured and user exists.
            if (user != null && await _emailService.IsConfiguredAsync())
            {
                var (_, _, resetUrl) = await _inviteService.CreateAsync(
                    user.Id, InviteService.TypePasswordReset, TimeSpan.FromHours(2), null);
                await _emailService.SendPasswordResetAsync(user.Email, user.Name, resetUrl);
            }

            return Ok(new { Message = "If the account exists, a reset link has been sent." });
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
                LinkedToEntra = !string.IsNullOrEmpty(user.EntraObjectId),
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            }
        };

        private async Task<Role> RequireRoleAsync(string roleName, string description)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role != null)
            {
                return role;
            }

            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                Description = description
            };
            _context.Roles.Add(role);
            return role;
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
