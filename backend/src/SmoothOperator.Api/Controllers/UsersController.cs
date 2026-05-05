using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Helpers;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class UsersController : ControllerBase
    {
        private static readonly IReadOnlyDictionary<string, string> RoleDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AppRoles.Owner] = "Root access and bootstrap account owner.",
                [AppRoles.Admin] = "Can create groups, invite users, vaults and credentials.",
                [AppRoles.TeamAdmin] = "Can create/manage connections in assigned vaults.",
                [AppRoles.User] = "Can only use connections in assigned vaults."
            };

        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public UsersController(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .OrderBy(u => u.Email)
                .ToListAsync();

            return Ok(users.Select(Project));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserListItemDto>> GetUser(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return Ok(Project(user));
        }

        [HttpGet("roles")]
        public ActionResult<IEnumerable<RoleCatalogItemDto>> GetRoles()
            => Ok(AppRoles.Defaults.Select(r => new RoleCatalogItemDto
            {
                Name = r,
                Description = RoleDescriptions[r]
            }));

        [HttpGet("{id}/vaults")]
        public async Task<ActionResult<object>> GetVaultAssignments(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return Ok(new
            {
                VaultIds = user.ConnectionGroups.Select(g => g.Id).OrderBy(v => v).ToList()
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Name = dto.Name.Trim();
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("user.updated", "User", user.Id.ToString(), new { user.Name });
            return NoContent();
        }

        /// <summary>
        /// Replaces the user's role. Smooth Operator enforces a single-role policy: the
        /// user's existing role assignment is cleared and replaced with the requested
        /// role. There is no additive role accumulation. The last active Owner cannot
        /// be demoted while still active.
        /// </summary>
        [HttpPut("{id}/role")]
        public async Task<IActionResult> SetRole(Guid id, [FromBody] SetUserRoleRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!AppRoles.IsKnown(dto.Role))
            {
                return BadRequest(new { message = $"Unsupported role. Allowed values: {string.Join(", ", AppRoles.Defaults)}." });
            }

            var normalizedRole = AppRoles.Normalize(dto.Role);

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var wasOwner = user.Roles.Any(r => r.Name == AppRoles.Owner);
            var becomesOwner = string.Equals(normalizedRole, AppRoles.Owner, StringComparison.OrdinalIgnoreCase);
            if (wasOwner && !becomesOwner && user.IsActive)
            {
                var otherActiveOwners = await _context.Users
                    .Include(u => u.Roles)
                    .CountAsync(u => u.Id != id && u.IsActive && u.Roles.Any(r => r.Name == AppRoles.Owner));
                if (otherActiveOwners == 0)
                {
                    return Conflict(new { message = "Cannot remove the Owner role from the last active Owner." });
                }
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == normalizedRole);
            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = normalizedRole,
                    Description = RoleDescriptions[normalizedRole]
                };
                _context.Roles.Add(role);
            }

            user.Roles.Clear();
            user.Roles.Add(role);

            await _context.SaveChangesAsync();
            await _audit.WriteAsync("user.role_changed", "User", user.Id.ToString(), new { role = normalizedRole });
            return NoContent();
        }

        [HttpPut("{id}/vaults")]
        public async Task<IActionResult> SetVaultAssignments(Guid id, [FromBody] SetUserVaultAssignmentsRequest dto)
        {
            var user = await _context.Users
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var requestedIds = (dto.VaultIds ?? new List<Guid>())
                .Where(v => v != Guid.Empty)
                .Distinct()
                .ToList();

            var groups = await _context.ConnectionGroups
                .Where(g => requestedIds.Contains(g.Id))
                .ToListAsync();

            if (groups.Count != requestedIds.Count)
            {
                return BadRequest(new { message = "One or more vault IDs are invalid." });
            }

            user.ConnectionGroups.Clear();
            foreach (var group in groups)
            {
                user.ConnectionGroups.Add(group);
            }

            await _context.SaveChangesAsync();
            await _audit.WriteAsync("user.vaults_updated", "User", user.Id.ToString(),
                new { vaultIds = requestedIds });
            return NoContent();
        }

        [HttpPatch("{id}/active")]
        public async Task<IActionResult> SetActive(Guid id, [FromBody] SetUserActiveRequest dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Don't allow disabling the last active Owner.
            if (!dto.IsActive)
            {
                var ownerCount = await _context.Users
                    .Include(u => u.Roles)
                    .Where(u => u.IsActive && u.Roles.Any(r => r.Name == AppRoles.Owner) && u.Id != id)
                    .CountAsync();
                var targetIsOwner = await _context.Users
                    .Include(u => u.Roles)
                    .AnyAsync(u => u.Id == id && u.Roles.Any(r => r.Name == AppRoles.Owner));
                if (targetIsOwner && ownerCount == 0)
                {
                    return Conflict(new { message = "Cannot disable the last active Owner." });
                }
            }

            user.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();
            await _audit.WriteAsync(
                dto.IsActive ? "user.enabled" : "user.disabled",
                "User",
                user.Id.ToString());
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idClaim, out var meId) && meId == id)
            {
                return Conflict(new { message = "You cannot delete your own account." });
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            // Don't allow deleting the last Owner.
            if (user.Roles.Any(r => r.Name == AppRoles.Owner))
            {
                var otherOwners = await _context.Users
                    .Include(u => u.Roles)
                    .CountAsync(u => u.Id != id && u.Roles.Any(r => r.Name == AppRoles.Owner));
                if (otherOwners == 0)
                {
                    return Conflict(new { message = "Cannot delete the last Owner." });
                }
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("user.deleted", "User", id.ToString(), new { user.Email });
            return NoContent();
        }

        // Allow any authenticated user to query their OWN effective vaults; only
        // Owner/Admin may query someone else's. Without [AllowAnonymous] the
        // controller-level [Authorize(Roles = OwnerOrAdmin)] would 403 a regular
        // user calling this for themselves (e.g. the My Access page).
        [AllowAnonymous]
        [HttpGet("{id}/effective-vaults")]
        public async Task<ActionResult<UserEffectiveVaultsDto>> GetEffectiveVaults(Guid id)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var isOwnerOrAdmin = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Admin);
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isSelf = Guid.TryParse(idClaim, out var meId) && meId == id;

            if (!isSelf && !isOwnerOrAdmin)
            {
                return Forbid();
            }

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    Direct = u.ConnectionGroups
                        .OrderBy(v => v.Name)
                        .Select(v => new EffectiveVaultDto { Id = v.Id, Name = v.Name, ParentGroupId = v.ParentGroupId })
                        .ToList(),
                    ViaGroups = u.Groups
                        .OrderBy(g => g.Name)
                        .Select(g => new EffectiveGroupVaultsDto
                        {
                            GroupId = g.Id,
                            GroupName = g.Name,
                            Vaults = g.Vaults
                                .OrderBy(v => v.Name)
                                .Select(v => new EffectiveVaultDto { Id = v.Id, Name = v.Name, ParentGroupId = v.ParentGroupId })
                                .ToList()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (user is null) return NotFound();

            var merged = user.Direct
                .Concat(user.ViaGroups.SelectMany(g => g.Vaults))
                .GroupBy(v => v.Id)
                .Select(grp => grp.First())
                .OrderBy(v => v.Name)
                .ToList();

            return Ok(new UserEffectiveVaultsDto
            {
                UserId = user.Id,
                Direct = user.Direct,
                ViaGroups = user.ViaGroups,
                Merged = merged
            });
        }

        // Self-service profile endpoints. The controller-level
        // [Authorize(Roles = OwnerOrAdmin)] would block any regular user from
        // editing their own profile, so [AllowAnonymous] + manual auth check
        // overrides that gate (same pattern as GetEffectiveVaults above).
        [AllowAnonymous]
        [HttpPut("me/profile")]
        public async Task<ActionResult<UserInfo>> UpdateMyProfile([FromBody] UpdateProfileRequest request)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var meId))
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == meId);
            if (user == null) return NotFound();

            user.Name = request.Name.Trim();

            var hasNewAvatar = !string.IsNullOrWhiteSpace(request.AvatarBase64);
            if (hasNewAvatar)
            {
                if (string.IsNullOrWhiteSpace(request.AvatarMimeType))
                {
                    return BadRequest(new { error = "AvatarMimeType is required when AvatarBase64 is set." });
                }

                byte[] decoded;
                try
                {
                    decoded = Convert.FromBase64String(request.AvatarBase64!);
                }
                catch (FormatException)
                {
                    return BadRequest(new { error = "AvatarBase64 is not a valid base64 string." });
                }

                const int MaxBytes = 1_048_576; // 1 MB
                if (decoded.Length > MaxBytes)
                {
                    return BadRequest(new { error = $"Avatar exceeds the {MaxBytes / 1024} KB size limit." });
                }

                user.AvatarBase64 = request.AvatarBase64;
                user.AvatarMimeType = request.AvatarMimeType;
            }

            await _context.SaveChangesAsync();
            await _audit.WriteAsync(
                hasNewAvatar ? "user.profile_updated_with_avatar" : "user.profile_updated",
                "User",
                user.Id.ToString(),
                new { user.Name, hasAvatar = !string.IsNullOrEmpty(user.AvatarBase64) });

            return Ok(new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
                SsoProviderType = user.SsoProviderType?.ToString(),
                AvatarUrl = UserHelper.BuildAvatarUrl(user),
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            });
        }

        [AllowAnonymous]
        [HttpDelete("me/avatar")]
        public async Task<ActionResult<UserInfo>> DeleteMyAvatar()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var meId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == meId);
            if (user == null) return NotFound();

            user.AvatarBase64 = null;
            user.AvatarMimeType = null;
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("user.avatar_removed", "User", user.Id.ToString(), new { });

            return Ok(new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
                SsoProviderType = user.SsoProviderType?.ToString(),
                AvatarUrl = null,
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            });
        }

        private static UserListItemDto Project(User user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            IsActive = user.IsActive,
            SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
            SsoProviderType = user.SsoProviderType?.ToString(),
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            CreatedAt = user.CreatedAt,
            Roles = user.Roles.Select(r => r.Name).OrderBy(r => r).ToList(),
            VaultIds = user.ConnectionGroups.Select(g => g.Id).OrderBy(v => v).ToList()
        };
    }
}
