using System;
using System.Collections.Generic;
using System.Linq;
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
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
    public class HostsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public HostsController(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HostDto>>> GetHosts()
        {
            var hosts = await _context.Hosts.ToListAsync();
            return Ok(hosts.Select(h => new HostDto
            {
                Id = h.Id,
                Name = h.Name,
                Address = h.Address
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HostDto>> GetHost(Guid id)
        {
            var host = await _context.Hosts.FindAsync(id);

            if (host == null)
            {
                return NotFound();
            }

            return new HostDto
            {
                Id = host.Id,
                Name = host.Name,
                Address = host.Address
            };
        }

        [HttpPost]
        public async Task<ActionResult<HostDto>> CreateHost([FromBody] CreateHostDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var host = new SmoothOperator.Domain.Models.Host
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Address = dto.Address
            };

            _context.Hosts.Add(host);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("host.created", "Host", host.Id.ToString(), new { host.Name, host.Address });

            return CreatedAtAction(nameof(GetHost), new { id = host.Id }, new HostDto
            {
                Id = host.Id,
                Name = host.Name,
                Address = host.Address
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHost(Guid id, [FromBody] CreateHostDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var host = await _context.Hosts.FindAsync(id);
            if (host == null)
            {
                return NotFound();
            }

            host.Name = dto.Name;
            host.Address = dto.Address;

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

            await _audit.WriteAsync("host.updated", "Host", id.ToString(), new { host.Name, host.Address });
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
            await _audit.WriteAsync("host.deleted", "Host", id.ToString(), new { host.Name });
            return NoContent();
        }

        private bool HostExists(Guid id)
        {
            return _context.Hosts.Any(e => e.Id == id);
        }
    }
}
