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
    [Route("api/groups")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class UserGroupsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;

        public UserGroupsController(AppDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserGroupDto>>> List()
        {
            var groups = await _db.UserGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .Include(g => g.Owner)
                .OrderBy(g => g.Name)
                .Select(g => new UserGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    OwnerUserId = g.OwnerUserId,
                    OwnerName = g.Owner != null ? g.Owner.Name : null,
                    CreatedAt = g.CreatedAt,
                    MemberCount = g.Members.Count,
                    VaultCount = g.Vaults.Count,
                    Members = g.Members
                        .OrderBy(m => m.Name)
                        .Select(m => new UserGroupMemberDto
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Email = m.Email,
                            IsActive = m.IsActive
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserGroupDto>> Get(Guid id)
        {
            var dto = await BuildGroupDtoAsync(id);
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        [HttpGet("{id}/vaults")]
        public async Task<ActionResult<IEnumerable<UserGroupVaultDto>>> ListVaults(Guid id)
        {
            var group = await _db.UserGroups
                .AsNoTracking()
                .Include(g => g.Vaults)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (group is null) return NotFound();

            var vaults = group.Vaults
                .OrderBy(v => v.Name)
                .Select(v => new UserGroupVaultDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    ParentGroupId = v.ParentGroupId
                })
                .ToList();

            return Ok(vaults);
        }

        [HttpPost]
        public async Task<ActionResult<UserGroupDto>> Create([FromBody] CreateUserGroupRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var name = req.Name.Trim();
            if (await NameExistsAsync(name, null))
            {
                return Conflict(new { message = $"A group named \"{name}\" already exists." });
            }

            if (req.OwnerUserId.HasValue)
            {
                var ownerExists = await _db.Users.AnyAsync(u => u.Id == req.OwnerUserId.Value);
                if (!ownerExists)
                    return BadRequest(new { message = "Owner user does not exist." });
            }

            var group = new UserGroup
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
                OwnerUserId = req.OwnerUserId
            };
            _db.UserGroups.Add(group);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("group.created", "UserGroup", group.Id.ToString(),
                new { group.Name, group.Description, group.OwnerUserId });

            var dto = await BuildGroupDtoAsync(group.Id);
            return CreatedAtAction(nameof(Get), new { id = group.Id }, dto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserGroupDto>> Update(Guid id, [FromBody] RenameUserGroupRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var group = await _db.UserGroups.FindAsync(id);
            if (group is null) return NotFound();

            var name = req.Name.Trim();
            var previousName = group.Name;
            if (!string.Equals(previousName, name, StringComparison.OrdinalIgnoreCase)
                && await NameExistsAsync(name, id))
            {
                return Conflict(new { message = $"A group named \"{name}\" already exists." });
            }

            if (req.OwnerUserId.HasValue)
            {
                var ownerExists = await _db.Users.AnyAsync(u => u.Id == req.OwnerUserId.Value);
                if (!ownerExists)
                    return BadRequest(new { message = "Owner user does not exist." });
            }

            var previousDescription = group.Description;
            var previousOwnerId = group.OwnerUserId;

            group.Name = name;
            group.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
            group.OwnerUserId = req.OwnerUserId;

            await _db.SaveChangesAsync();
            await _audit.WriteAsync("group.updated", "UserGroup", group.Id.ToString(),
                new
                {
                    previousName,
                    newName = group.Name,
                    previousDescription,
                    newDescription = group.Description,
                    previousOwnerId,
                    newOwnerId = group.OwnerUserId
                });

            var dto = await BuildGroupDtoAsync(group.Id);
            return Ok(dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var group = await _db.UserGroups
                .Include(g => g.Vaults)
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (group is null) return NotFound();

            if (group.Vaults.Any())
                return Conflict(new { message = "Cannot delete a group that is assigned to vaults. Remove vault assignments first." });

            var snapshot = new { group.Name, MemberCount = group.Members.Count };
            _db.UserGroups.Remove(group);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("group.deleted", "UserGroup", id.ToString(), snapshot);
            return NoContent();
        }

        [HttpPut("{id}/members")]
        public async Task<IActionResult> SetMembers(Guid id, [FromBody] SetUserGroupMembersRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (req.UserIds == null) return BadRequest(new { message = "UserIds is required." });

            var group = await _db.UserGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (group is null) return NotFound();

            var requestedIds = req.UserIds.Distinct().ToList();
            var userInfos = await _db.Users
                .Where(u => requestedIds.Contains(u.Id))
                .Select(u => new { u.Id, u.IsActive })
                .ToListAsync();

            if (userInfos.Count != requestedIds.Count)
            {
                var foundIds = userInfos.Select(u => u.Id).ToHashSet();
                var missing = requestedIds.Where(uid => !foundIds.Contains(uid)).ToList();
                return BadRequest(new
                {
                    message = "One or more users do not exist.",
                    missingUserIds = missing
                });
            }

            var inactiveIds = userInfos.Where(u => !u.IsActive).Select(u => u.Id).ToList();
            var previousMemberIds = group.Members.Select(m => m.Id).ToList();

            var currentMemberIds = previousMemberIds.ToHashSet();
            var newRequestedIds = requestedIds.ToHashSet();

            if (group.Members is System.Collections.Generic.List<User> list)
            {
                list.RemoveAll(m => !newRequestedIds.Contains(m.Id));
            }
            else
            {
                var toRemove = group.Members.Where(m => !newRequestedIds.Contains(m.Id)).ToList();
                foreach (var user in toRemove)
                    group.Members.Remove(user);
            }

            var toAddIds = newRequestedIds.Where(userId => !currentMemberIds.Contains(userId)).ToList();

            foreach (var newId in toAddIds)
            {
                var dummyUser = new User { Id = newId };
                _db.Users.Attach(dummyUser);
                group.Members.Add(dummyUser);
            }

            await _db.SaveChangesAsync();
            await _audit.WriteAsync("group.members.updated", "UserGroup", id.ToString(),
                new
                {
                    previousMemberIds,
                    newMemberIds = requestedIds,
                    inactiveAssigned = inactiveIds
                });

            return NoContent();
        }

        private async Task<bool> NameExistsAsync(string name, Guid? excludeId)
        {
            var query = _db.UserGroups.AsNoTracking()
                .Where(g => g.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue) query = query.Where(g => g.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        private async Task<UserGroupDto?> BuildGroupDtoAsync(Guid id)
        {
            return await _db.UserGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .Include(g => g.Owner)
                .Where(g => g.Id == id)
                .Select(g => new UserGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    OwnerUserId = g.OwnerUserId,
                    OwnerName = g.Owner != null ? g.Owner.Name : null,
                    CreatedAt = g.CreatedAt,
                    MemberCount = g.Members.Count,
                    VaultCount = g.Vaults.Count,
                    Members = g.Members
                        .OrderBy(m => m.Name)
                        .Select(m => new UserGroupMemberDto
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Email = m.Email,
                            IsActive = m.IsActive
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();
        }
    }
}
