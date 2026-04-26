using System;
using System.Collections.Generic;
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

namespace Backend.Controllers
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

        private static UserListItemDto Project(User user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            IsActive = user.IsActive,
            LinkedToEntra = !string.IsNullOrEmpty(user.EntraObjectId),
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            CreatedAt = user.CreatedAt,
            Roles = user.Roles.Select(r => r.Name).OrderBy(r => r).ToList(),
            VaultIds = user.ConnectionGroups.Select(g => g.Id).OrderBy(v => v).ToList()
        };
    }
}
