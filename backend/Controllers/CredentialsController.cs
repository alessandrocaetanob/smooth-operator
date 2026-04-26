using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
    public class CredentialsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditService _audit;

        public CredentialsController(AppDbContext context, IEncryptionService encryptionService, IAuditService audit)
        {
            _context = context;
            _encryptionService = encryptionService;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CredentialDto>>> GetCredentials()
        {
            // Do not return the encrypted secret in lists
            var credentials = await _context.Credentials.ToListAsync();
            return Ok(credentials.Select(c => new CredentialDto
            {
                Id = c.Id,
                Name = c.Name,
                Username = c.Username,
                CredentialType = c.CredentialType
            }));
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<ActionResult<CredentialDto>> CreateCredential([FromBody] CreateCredentialDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var credential = new Credential
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Username = dto.Username,
                CredentialType = dto.CredentialType,
                EncryptedSecret = _encryptionService.Encrypt(dto.Secret)
            };

            _context.Credentials.Add(credential);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("credential.created", "Credential", credential.Id.ToString(),
                new { credential.Name, credential.Username, credential.CredentialType });

            // Return without secret
            return CreatedAtAction(nameof(GetCredentials), new { id = credential.Id },
                new CredentialDto
                {
                    Id = credential.Id,
                    Name = credential.Name,
                    Username = credential.Username,
                    CredentialType = credential.CredentialType
                });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> UpdateCredential(Guid id, [FromBody] CreateCredentialDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var credential = await _context.Credentials.FindAsync(id);
            if (credential == null)
            {
                return NotFound();
            }

            credential.Name = dto.Name;
            credential.Username = dto.Username;
            credential.CredentialType = dto.CredentialType;

            // Only update secret if provided
            var secretRotated = false;
            if (!string.IsNullOrEmpty(dto.Secret))
            {
                credential.EncryptedSecret = _encryptionService.Encrypt(dto.Secret);
                secretRotated = true;
            }

            await _context.SaveChangesAsync();
            await _audit.WriteAsync("credential.updated", "Credential", credential.Id.ToString(),
                new { credential.Name, credential.Username, credential.CredentialType, secretRotated });
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> DeleteCredential(Guid id)
        {
            var credential = await _context.Credentials.FindAsync(id);
            if (credential == null)
            {
                return NotFound();
            }

            _context.Credentials.Remove(credential);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("credential.deleted", "Credential", id.ToString(),
                new { credential.Name });
            return NoContent();
        }
    }

    public class CreateCredentialDto
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(255, ErrorMessage = "Username cannot exceed 255 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Secret is required")]
        [StringLength(4096, MinimumLength = 1, ErrorMessage = "Secret must be between 1 and 4096 characters")]
        public string Secret { get; set; } = string.Empty;

        [Required(ErrorMessage = "Credential type is required")]
        [RegularExpression("^(password|private_key|api_token)$", ErrorMessage = "Credential type must be one of: password, private_key, api_token")]
        public string CredentialType { get; set; } = "password";
    }
}
