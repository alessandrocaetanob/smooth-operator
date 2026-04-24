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
    public class HostsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HostsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Backend.Models.Host>>> GetHosts()
        {
            return await _context.Hosts.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Backend.Models.Host>> GetHost(Guid id)
        {
            var host = await _context.Hosts.FindAsync(id);

            if (host == null)
            {
                return NotFound();
            }

            return host;
        }

        [HttpPost]
        public async Task<ActionResult<Backend.Models.Host>> CreateHost(Backend.Models.Host host)
        {
            host.Id = Guid.NewGuid();
            _context.Hosts.Add(host);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetHost), new { id = host.Id }, host);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHost(Guid id, Backend.Models.Host host)
        {
            if (id != host.Id)
            {
                return BadRequest();
            }

            _context.Entry(host).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HostExists(id))
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
        public async Task<IActionResult> DeleteHost(Guid id)
        {
            var host = await _context.Hosts.FindAsync(id);
            if (host == null)
            {
                return NotFound();
            }

            _context.Hosts.Remove(host);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool HostExists(Guid id)
        {
            return _context.Hosts.Any(e => e.Id == id);
        }
    }
}
