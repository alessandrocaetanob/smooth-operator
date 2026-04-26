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
using System.Security.Cryptography;

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
                CredentialType = c.CredentialType,
                PublicKey = c.PublicKey
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
                PublicKey = dto.PublicKey,
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
                    CredentialType = credential.CredentialType,
                    PublicKey = credential.PublicKey
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
            credential.PublicKey = dto.PublicKey;

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
        [HttpPost("generate-ssh")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public ActionResult<GenerateSshKeyResponse> GenerateSshKey([FromBody] GenerateSshKeyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string privateKey;
            string publicKey;

            if (request.KeyType == "rsa")
            {
                using var rsa = RSA.Create(4096);
                privateKey = rsa.ExportRSAPrivateKeyPem();
                publicKey = EncodeRsaPublicKeyOpenSsh(rsa);
            }
            else if (request.KeyType == "ecdsa")
            {
                using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                privateKey = ecdsa.ExportECPrivateKeyPem();
                publicKey = EncodeEcdsaPublicKeyOpenSsh(ecdsa);
            }
            else
            {
                return BadRequest("Unsupported key type.");
            }

            return Ok(new GenerateSshKeyResponse
            {
                PrivateKey = privateKey,
                PublicKey = publicKey
            });
        }

        // Encodes an RSA public key in OpenSSH authorized_keys format: "ssh-rsa <base64>"
        private static string EncodeRsaPublicKeyOpenSsh(RSA rsa)
        {
            var p = rsa.ExportParameters(false);
            using var ms = new MemoryStream();
            SshWriteString(ms, "ssh-rsa");
            SshWriteMpInt(ms, p.Exponent!);
            SshWriteMpInt(ms, p.Modulus!);
            return $"ssh-rsa {Convert.ToBase64String(ms.ToArray())}";
        }

        // Encodes an ECDSA P-256 public key in OpenSSH authorized_keys format
        private static string EncodeEcdsaPublicKeyOpenSsh(ECDsa ecdsa)
        {
            var p = ecdsa.ExportParameters(false);
            var x = NormalizeCoordinate(p.Q.X!);
            var y = NormalizeCoordinate(p.Q.Y!);
            var point = new byte[1 + x.Length + y.Length];
            point[0] = 0x04; // uncompressed point
            Buffer.BlockCopy(x, 0, point, 1, x.Length);
            Buffer.BlockCopy(y, 0, point, 1 + x.Length, y.Length);
            using var ms = new MemoryStream();
            SshWriteString(ms, "ecdsa-sha2-nistp256");
            SshWriteString(ms, "nistp256");
            SshWriteBytes(ms, point);
            return $"ecdsa-sha2-nistp256 {Convert.ToBase64String(ms.ToArray())}";
        }

        // Ensures a P-256 coordinate is exactly 32 bytes (pad or trim leading zeros)
        private static byte[] NormalizeCoordinate(byte[] b)
        {
            if (b.Length == 32) return b;
            if (b.Length > 32) return b[^32..];
            var padded = new byte[32];
            Buffer.BlockCopy(b, 0, padded, 32 - b.Length, b.Length);
            return padded;
        }

        private static void SshWriteLen(Stream s, int len)
        {
            s.WriteByte((byte)(len >> 24));
            s.WriteByte((byte)(len >> 16));
            s.WriteByte((byte)(len >> 8));
            s.WriteByte((byte)len);
        }

        private static void SshWriteBytes(Stream s, byte[] data)
        {
            SshWriteLen(s, data.Length);
            s.Write(data, 0, data.Length);
        }

        private static void SshWriteString(Stream s, string value)
        {
            SshWriteBytes(s, System.Text.Encoding.ASCII.GetBytes(value));
        }

        // Writes a multi-precision integer (mpint): prepends 0x00 if the high bit is set
        private static void SshWriteMpInt(Stream s, byte[] data)
        {
            int start = 0;
            while (start < data.Length - 1 && data[start] == 0) start++;
            bool needsPad = (data[start] & 0x80) != 0;
            int len = data.Length - start + (needsPad ? 1 : 0);
            SshWriteLen(s, len);
            if (needsPad) s.WriteByte(0x00);
            s.Write(data, start, data.Length - start);
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

        public string? PublicKey { get; set; }
    }
}
