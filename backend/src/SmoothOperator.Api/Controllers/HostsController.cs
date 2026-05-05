using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Hosts.Commands;
using SmoothOperator.Application.Features.Hosts.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
    public class HostsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HostsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetHosts()
        {
            var result = await _mediator.Send(new GetHostsQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHost(Guid id)
        {
            var result = await _mediator.Send(new GetHostQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateHost([FromBody] CreateHostDto dto)
        {
            var result = await _mediator.Send(new CreateHostCommand(dto, HttpContext.User));
            return CreatedAtAction(nameof(GetHost), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHost(Guid id, [FromBody] CreateHostDto dto)
        {
            await _mediator.Send(new UpdateHostCommand(id, dto, HttpContext.User));
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHost(Guid id)
        {
            await _mediator.Send(new DeleteHostCommand(id, HttpContext.User));
            return NoContent();
        }
    }
}
