using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Data;
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
        public async Task<ActionResult<IEnumerable<Connection>>> GetConnections()
        {
            // Includes basic related info
            return await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.ConnectionGroup)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Connection>> GetConnection(Guid id)
        {
            var connection = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.ConnectionGroup)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null)
            {
                return NotFound();
            }

            return connection;
        }

        [HttpPost]
        public async Task<ActionResult<Connection>> CreateConnection(Connection connection)
        {
            connection.Id = Guid.NewGuid();
            _context.Connections.Add(connection);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetConnection), new { id = connection.Id }, connection);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateConnection(Guid id, Connection connection)
        {
            if (id != connection.Id)
            {
                return BadRequest();
            }

            _context.Entry(connection).State = EntityState.Modified;

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
