using System;
using System.Collections.Generic;
using System.Linq;
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
    [Route("api/vaults")]
    [Authorize]
    public class ConnectionGroupsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAccessControlService _access;
        private readonly IAuditService _audit;

        public ConnectionGroupsController(AppDbContext context, IAccessControlService access, IAuditService audit)
        {
            _context = context;
            _access = access;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConnectionGroupDto>>> GetVaults()
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var vaults = await _access.ApplyVaultScope(_context.ConnectionGroups.AsNoTracking(), profile)
                .OrderBy(v => v.Name)
                .Select(v => new ConnectionGroupDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    ParentGroupId = v.ParentGroupId
                })
                .ToListAsync();

            return Ok(vaults);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<ActionResult<ConnectionGroupDto>> CreateVault([FromBody] CreateConnectionGroupDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (dto.ParentGroupId.HasValue)
            {
                var parentExists = await _context.ConnectionGroups.AnyAsync(g => g.Id == dto.ParentGroupId.Value);
                if (!parentExists)
                {
                    return BadRequest(new { message = "Parent vault does not exist." });
                }
            }

            var name = dto.Name.Trim();
            if (await VaultNameExistsAsync(name, null))
            {
                return Conflict(new { message = $"A vault named \"{name}\" already exists." });
            }

            var vault = new ConnectionGroup
            {
                Id = Guid.NewGuid(),
                Name = name,
                ParentGroupId = dto.ParentGroupId
            };

            _context.ConnectionGroups.Add(vault);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("vault.created", "ConnectionGroup", vault.Id.ToString(),
                new { vault.Name, vault.ParentGroupId });

            return CreatedAtAction(nameof(GetVaults), new { id = vault.Id }, new ConnectionGroupDto
            {
                Id = vault.Id,
                Name = vault.Name,
                ParentGroupId = vault.ParentGroupId
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> UpdateVault(Guid id, [FromBody] CreateConnectionGroupDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.ParentGroupId == id)
            {
                return BadRequest(new { message = "A vault cannot be its own parent." });
            }

            if (dto.ParentGroupId.HasValue)
            {
                var parentExists = await _context.ConnectionGroups.AnyAsync(g => g.Id == dto.ParentGroupId.Value);
                if (!parentExists)
                {
                    return BadRequest(new { message = "Parent vault does not exist." });
                }
            }

            var vault = await _context.ConnectionGroups.FindAsync(id);
            if (vault == null) return NotFound();

            var name = dto.Name.Trim();
            if (!string.Equals(vault.Name, name, StringComparison.OrdinalIgnoreCase)
                && await VaultNameExistsAsync(name, id))
            {
                return Conflict(new { message = $"A vault named \"{name}\" already exists." });
            }

            vault.Name = name;
            vault.ParentGroupId = dto.ParentGroupId;

            await _context.SaveChangesAsync();
            await _audit.WriteAsync("vault.updated", "ConnectionGroup", id.ToString(),
                new { vault.Name, vault.ParentGroupId });
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> DeleteVault(Guid id)
        {
            var vault = await _context.ConnectionGroups
                .Include(v => v.Connections)
                .Include(v => v.Users)
                .Include(v => v.Groups)
                .FirstOrDefaultAsync(v => v.Id == id);
            if (vault == null) return NotFound();

            if (vault.Connections.Any())
            {
                return Conflict(new { message = "Cannot delete a vault that still has connections." });
            }

            if (vault.Users.Any())
            {
                return Conflict(new { message = "Cannot delete a vault that is assigned to users." });
            }

            if (vault.Groups.Any())
            {
                return Conflict(new { message = "Cannot delete a vault that is assigned to groups." });
            }

            _context.ConnectionGroups.Remove(vault);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("vault.deleted", "ConnectionGroup", id.ToString(), new { vault.Name });
            return NoContent();
        }

        [HttpGet("{id}/assignments")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<ActionResult<VaultAssignmentsDto>> GetAssignments(Guid id)
        {
            var vault = await _context.ConnectionGroups
                .AsNoTracking()
                .Include(v => v.Users)
                .Include(v => v.Groups)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vault == null) return NotFound();

            return Ok(new VaultAssignmentsDto
            {
                UserIds = vault.Users.Select(u => u.Id).ToList(),
                GroupIds = vault.Groups.Select(g => g.Id).ToList()
            });
        }

        [HttpPut("{id}/assignments")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> SetAssignments(Guid id, [FromBody] VaultAssignmentsDto dto)
        {
            var vault = await _context.ConnectionGroups
                .Include(v => v.Users)
                .Include(v => v.Groups)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vault == null) return NotFound();

            var users = await _context.Users
                .Where(u => dto.UserIds.Contains(u.Id))
                .ToListAsync();

            var groups = await _context.UserGroups
                .Where(g => dto.GroupIds.Contains(g.Id))
                .ToListAsync();

            vault.Users.Clear();
            foreach (var u in users) vault.Users.Add(u);

            vault.Groups.Clear();
            foreach (var g in groups) vault.Groups.Add(g);

            await _context.SaveChangesAsync();
            await _audit.WriteAsync("vault.assignments.updated", "ConnectionGroup", id.ToString(),
                new { UserCount = users.Count, GroupCount = groups.Count });

            return NoContent();
        }

        [HttpGet("{id}/effective-users")]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<ActionResult<VaultEffectiveUsersDto>> GetEffectiveUsers(Guid id)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            // TeamAdmins can only inspect vaults they have access to.
            if (!profile.IsOwnerOrAdmin && !profile.VaultIds.Contains(id))
            {
                return Forbid();
            }

            var vault = await _context.ConnectionGroups
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(v => new
                {
                    v.Id,
                    v.Name,
                    DirectUsers = v.Users.Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.IsActive,
                        Roles = u.Roles.Select(r => r.Name).ToList()
                    }).ToList(),
                    GroupUsers = v.Groups.SelectMany(g => g.Members.Select(u => new
                    {
                        UserId = u.Id,
                        u.Name,
                        u.Email,
                        u.IsActive,
                        Roles = u.Roles.Select(r => r.Name).ToList(),
                        GroupId = g.Id
                    })).ToList()
                })
                .FirstOrDefaultAsync();

            if (vault == null) return NotFound();

            var directIds = vault.DirectUsers.Select(u => u.Id).ToHashSet();
            var groupedByUser = vault.GroupUsers
                .GroupBy(gu => gu.UserId)
                .ToDictionary(grp => grp.Key, grp => grp.ToList());

            var allUserIds = directIds.Union(groupedByUser.Keys).ToList();

            var users = new List<EffectiveUserSourceDto>();
            foreach (var uid in allUserIds)
            {
                var direct = vault.DirectUsers.FirstOrDefault(u => u.Id == uid);
                var viaGroups = groupedByUser.TryGetValue(uid, out var list) ? list : null;
                var name = direct?.Name ?? viaGroups?[0].Name ?? string.Empty;
                var email = direct?.Email ?? viaGroups?[0].Email ?? string.Empty;
                var isActive = direct?.IsActive ?? viaGroups?[0].IsActive ?? false;
                var roles = direct?.Roles ?? viaGroups?[0].Roles ?? new List<string>();

                users.Add(new EffectiveUserSourceDto
                {
                    Id = uid,
                    Name = name,
                    Email = email,
                    IsActive = isActive,
                    Roles = roles,
                    DirectAssignment = directIds.Contains(uid),
                    ViaGroupIds = viaGroups?.Select(v => v.GroupId).Distinct().ToList() ?? new List<Guid>()
                });
            }

            return Ok(new VaultEffectiveUsersDto
            {
                VaultId = vault.Id,
                VaultName = vault.Name,
                Users = users.OrderBy(u => u.Name).ToList()
            });
        }

        private async Task<bool> VaultNameExistsAsync(string name, Guid? excludeId)
        {
            var query = _context.ConnectionGroups.AsNoTracking()
                .Where(v => EF.Functions.ILike(v.Name, name));
            if (excludeId.HasValue) query = query.Where(v => v.Id != excludeId.Value);
            return await query.AnyAsync();
        }
    }

    public class VaultAssignmentsDto
    {
        public List<Guid> UserIds { get; set; } = new();
        public List<Guid> GroupIds { get; set; } = new();
    }
}
