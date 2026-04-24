using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
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

        public ConnectionsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetConnections()
        {
            // Includes basic related info but excludes credentials
            var connections = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.ConnectionGroup)
                .ToListAsync();

            // Return without credential information
            return Ok(connections.Select(c => new
            {
                c.Id,
                c.Name,
                c.Protocol,
                c.HostId,
                c.ConnectionGroupId,
                c.Settings,
                Host = c.Host != null ? new { c.Host.Id, c.Host.Name, c.Host.Address } : null,
                ConnectionGroup = c.ConnectionGroup != null ? new { c.ConnectionGroup.Id, c.ConnectionGroup.Name } : null
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetConnection(Guid id)
        {
            var connection = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.ConnectionGroup)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null)
            {
                return NotFound();
            }

            // Return without credential information
            return Ok(new
            {
                connection.Id,
                connection.Name,
                connection.Protocol,
                connection.HostId,
                connection.ConnectionGroupId,
                connection.Settings,
                Host = connection.Host != null ? new { connection.Host.Id, connection.Host.Name, connection.Host.Address } : null,
                ConnectionGroup = connection.ConnectionGroup != null ? new { connection.ConnectionGroup.Id, connection.ConnectionGroup.Name } : null
            });
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

            return NoContent();
        }

        private bool ConnectionExists(Guid id)
        {
            return _context.Connections.Any(e => e.Id == id);
        }
    }
}
