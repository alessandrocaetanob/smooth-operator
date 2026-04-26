using Backend.Data;
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

        public UserGroupsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var groups = await _db.UserGroups
                .Include(g => g.Members)
                .OrderBy(g => g.Name)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.CreatedAt,
                    MemberCount = g.Members.Count,
                    Members = g.Members.Select(m => new { m.Id, m.Name, m.Email })
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGroupRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Group name is required." });

            var group = new UserGroup { Name = req.Name.Trim() };
            _db.UserGroups.Add(group);
            await _db.SaveChangesAsync();
            return Ok(new { group.Id, group.Name, group.CreatedAt, MemberCount = 0, Members = Array.Empty<object>() });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateGroupRequest req)
        {
            var group = await _db.UserGroups.FindAsync(id);
            if (group is null) return NotFound();

            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Group name is required." });

            group.Name = req.Name.Trim();
            await _db.SaveChangesAsync();
            return Ok(new { group.Id, group.Name, group.CreatedAt });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var group = await _db.UserGroups
                .Include(g => g.Vaults)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (group is null) return NotFound();

            if (group.Vaults.Any())
                return Conflict(new { message = "Cannot delete a group that is assigned to vaults. Remove vault assignments first." });

            _db.UserGroups.Remove(group);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/members")]
        public async Task<IActionResult> SetMembers(Guid id, [FromBody] SetMembersRequest req)
        {
            var group = await _db.UserGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group is null) return NotFound();

            var users = await _db.Users
                .Where(u => req.UserIds.Contains(u.Id))
                .ToListAsync();

            group.Members.Clear();
            foreach (var user in users)
                group.Members.Add(user);

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    public record CreateGroupRequest(string Name);
    public record SetMembersRequest(List<Guid> UserIds);
}
