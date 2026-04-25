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
    [Route("api/[controller]")]
    [Authorize]
    public class ConnectionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public ConnectionsController(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        private static ConnectionDto Project(Connection c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Protocol = c.Protocol,
            HostId = c.HostId,
            CredentialId = c.CredentialId,
            ConnectionGroupId = c.ConnectionGroupId,
            Settings = c.Settings,
            Host = c.Host == null ? null : new HostDto
            {
                Id = c.Host.Id,
                Name = c.Host.Name,
                Address = c.Host.Address
            },
            ConnectionGroup = c.ConnectionGroup == null ? null : new ConnectionGroupDto
            {
                Id = c.ConnectionGroup.Id,
                Name = c.ConnectionGroup.Name
            }
        };

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConnectionDto>>> GetConnections()
        {
            var connections = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.ConnectionGroup)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(connections.Select(Project));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ConnectionDto>> GetConnection(Guid id)
        {
            var connection = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.ConnectionGroup)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null) return NotFound();
            return Ok(Project(connection));
        }

        [HttpPost]
        public async Task<ActionResult<ConnectionDto>> CreateConnection([FromBody] CreateConnectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Protocol = dto.Protocol,
                HostId = dto.HostId,
                CredentialId = dto.CredentialId,
                ConnectionGroupId = dto.ConnectionGroupId,
                Settings = dto.Settings
            };

            _context.Connections.Add(connection);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("connection.created", "Connection", connection.Id.ToString(),
                new { connection.Name, connection.Protocol, connection.HostId });

            return CreatedAtAction(nameof(GetConnection), new { id = connection.Id }, new ConnectionDto
            {
                Id = connection.Id,
                Name = connection.Name,
                Protocol = connection.Protocol,
                HostId = connection.HostId,
                CredentialId = connection.CredentialId,
                ConnectionGroupId = connection.ConnectionGroupId,
                Settings = connection.Settings
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateConnection(Guid id, [FromBody] CreateConnectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var connection = await _context.Connections.FindAsync(id);
            if (connection == null)
            {
                return NotFound();
            }

            connection.Name = dto.Name;
            connection.Protocol = dto.Protocol;
            connection.HostId = dto.HostId;
            connection.CredentialId = dto.CredentialId;
            connection.ConnectionGroupId = dto.ConnectionGroupId;
            connection.Settings = dto.Settings;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ConnectionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            await _audit.WriteAsync("connection.updated", "Connection", id.ToString(),
                new { connection.Name, connection.Protocol });
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConnection(Guid id)
        {
            var connection = await _context.Connections.FindAsync(id);
            if (connection == null)
            {
                return NotFound();
            }

            _context.Connections.Remove(connection);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("connection.deleted", "Connection", id.ToString(),
                new { connection.Name });
            return NoContent();
        }

        private bool ConnectionExists(Guid id)
        {
            return _context.Connections.Any(e => e.Id == id);
        }
    }
}
