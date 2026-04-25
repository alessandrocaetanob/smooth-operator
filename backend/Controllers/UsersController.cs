using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Data;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
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
                .OrderBy(u => u.Email)
                .ToListAsync();

            return Ok(users.Select(u => new UserListItemDto
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Name,
                IsActive = u.IsActive,
                LinkedToEntra = !string.IsNullOrEmpty(u.EntraObjectId),
                HasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                CreatedAt = u.CreatedAt,
                Roles = u.Roles.Select(r => r.Name).ToList()
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserListItemDto>> GetUser(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return Ok(new UserListItemDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                IsActive = user.IsActive,
                LinkedToEntra = !string.IsNullOrEmpty(user.EntraObjectId),
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                CreatedAt = user.CreatedAt,
                Roles = user.Roles.Select(r => r.Name).ToList()
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
                    .Where(u => u.IsActive && u.Roles.Any(r => r.Name == "Owner") && u.Id != id)
                    .CountAsync();
                var targetIsOwner = await _context.Users
                    .Include(u => u.Roles)
                    .AnyAsync(u => u.Id == id && u.Roles.Any(r => r.Name == "Owner"));
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
            if (user.Roles.Any(r => r.Name == "Owner"))
            {
                var otherOwners = await _context.Users
                    .Include(u => u.Roles)
                    .CountAsync(u => u.Id != id && u.Roles.Any(r => r.Name == "Owner"));
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
    }
}
