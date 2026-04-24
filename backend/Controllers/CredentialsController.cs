using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CredentialsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public CredentialsController(AppDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetCredentials()
        {
            // Do not return the encrypted secret in lists
            var credentials = await _context.Credentials.ToListAsync();
            var result = new List<object>();
            foreach (var cred in credentials)
            {
                result.Add(new { cred.Id, cred.Name, cred.Username, cred.CredentialType });
            }
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<Credential>> CreateCredential([FromBody] CreateCredentialDto dto)
        {
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

            // Return without secret
            return CreatedAtAction(nameof(GetCredentials), new { id = credential.Id },
                new { credential.Id, credential.Name, credential.Username, credential.CredentialType });
        }

        // Additional endpoints (Update, Delete) omitted for brevity but follow the same pattern
    }

    public class CreateCredentialDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string CredentialType { get; set; } = "password";
    }
}
