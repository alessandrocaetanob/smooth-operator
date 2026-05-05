using System;
using System.Security.Claims;
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
    [Route("api/settings/smtp")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SmtpSettingsController : ControllerBase
    {
        private static readonly Guid SingletonId = new("11111111-1111-1111-1111-111111111111");

        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryption;
        private readonly IEmailService _email;
        private readonly IAuditService _audit;

        public SmtpSettingsController(
            AppDbContext context,
            IEncryptionService encryption,
            IEmailService email,
            IAuditService audit)
        {
            _context = context;
            _encryption = encryption;
            _email = email;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<SmtpSettingsDto>> Get()
        {
            var s = await _context.SmtpSettings.AsNoTracking().FirstOrDefaultAsync();
            return Ok(ToDto(s));
        }

        [HttpPut]
        public async Task<ActionResult<SmtpSettingsDto>> Update([FromBody] UpdateSmtpSettingsRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var s = await _context.SmtpSettings.FirstOrDefaultAsync();
            var creating = s == null;
            if (s == null)
            {
                s = new SmtpSettings { Id = SingletonId };
                _context.SmtpSettings.Add(s);
            }

            s.Host = request.Host.Trim();
            s.Port = request.Port;
            s.UseSsl = request.UseSsl;
            s.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
            s.FromAddress = request.FromAddress.Trim();
            s.FromName = string.IsNullOrWhiteSpace(request.FromName) ? null : request.FromName.Trim();
            s.Enabled = request.Enabled;
            s.UpdatedAt = DateTime.UtcNow;

            // Password handling: null => keep existing; "" => clear; non-empty => replace.
            if (request.Password != null)
            {
                if (request.Password.Length == 0)
                {
                    s.EncryptedPassword = null;
                }
                else
                {
                    s.EncryptedPassword = _encryption.Encrypt(request.Password);
                }
            }

            await _context.SaveChangesAsync();
            await _audit.WriteAsync(creating ? "smtp.created" : "smtp.updated", "SmtpSettings", s.Id.ToString(),
                new { s.Host, s.Port, s.UseSsl, s.FromAddress, s.Enabled });

            return Ok(ToDto(s));
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test([FromBody] SmtpTestRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!await _email.IsConfiguredAsync())
            {
                return BadRequest(new { message = "SMTP relay is not configured." });
            }

            var result = await _email.SendTestAsync(request.ToEmail);
            await _audit.WriteAsync("smtp.test_sent", "SmtpSettings", SingletonId.ToString(),
                new { to = request.ToEmail, success = result.Success });

            if (!result.Success) return StatusCode(502, new { message = result.Error ?? "SMTP send failed." });
            return Ok(new { success = true });
        }

        private static SmtpSettingsDto ToDto(SmtpSettings? s)
        {
            if (s == null)
            {
                return new SmtpSettingsDto { Configured = false };
            }

            return new SmtpSettingsDto
            {
                Configured = !string.IsNullOrWhiteSpace(s.Host) && !string.IsNullOrWhiteSpace(s.FromAddress),
                Host = s.Host,
                Port = s.Port,
                UseSsl = s.UseSsl,
                Username = s.Username,
                FromAddress = s.FromAddress,
                FromName = s.FromName,
                Enabled = s.Enabled,
                HasPassword = !string.IsNullOrEmpty(s.EncryptedPassword),
                UpdatedAt = s.UpdatedAt,
            };
        }
    }
}
