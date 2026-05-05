using System;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/settings/system")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SystemSettingsController : ControllerBase
    {
        private static readonly Guid SingletonId = new("22222222-2222-2222-2222-222222222222");

        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public SystemSettingsController(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<SystemSettingsDto>> Get()
        {
            var s = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            if (s == null)
            {
                return Ok(new SystemSettingsDto { AuditLogRetentionDays = 0, UpdatedAt = DateTime.UtcNow });
            }
            return Ok(ToDto(s));
        }

        [HttpPut]
        public async Task<ActionResult<SystemSettingsDto>> Update([FromBody] UpdateSystemSettingsRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var s = await _context.SystemSettings.FirstOrDefaultAsync();
            var creating = s == null;
            if (s == null)
            {
                s = new SystemSettings { Id = SingletonId };
                _context.SystemSettings.Add(s);
            }

            var previous = s.AuditLogRetentionDays;
            s.AuditLogRetentionDays = request.AuditLogRetentionDays;
            s.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _audit.WriteAsync(
                creating ? "system.settings_created" : "system.settings_updated",
                "SystemSettings",
                s.Id.ToString(),
                new { previous, current = s.AuditLogRetentionDays });

            return Ok(ToDto(s));
        }

        private static SystemSettingsDto ToDto(SystemSettings s) => new()
        {
            AuditLogRetentionDays = s.AuditLogRetentionDays,
            UpdatedAt = s.UpdatedAt,
        };
    }
}
