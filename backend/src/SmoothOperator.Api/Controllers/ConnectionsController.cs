using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Connections.Commands;
using SmoothOperator.Application.Features.Connections.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConnectionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ConnectionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetConnections()
        {
            var result = await _mediator.Send(new GetConnectionsQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetConnection(Guid id)
        {
            var result = await _mediator.Send(new GetConnectionQuery(id, HttpContext.User));
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<IActionResult> CreateConnection([FromBody] CreateConnectionDto dto)
        {
            var result = await _mediator.Send(new CreateConnectionCommand(dto, HttpContext.User));
            return CreatedAtAction(nameof(GetConnection), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<IActionResult> UpdateConnection(Guid id, [FromBody] CreateConnectionDto dto)
        {
            await _mediator.Send(new UpdateConnectionCommand(id, dto, HttpContext.User));
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<IActionResult> DeleteConnection(Guid id)
        {
            await _mediator.Send(new DeleteConnectionCommand(id, HttpContext.User));
            return NoContent();
        }

        [HttpGet("my-last-connected")]
        public async Task<IActionResult> GetMyLastConnected()
        {
            var result = await _mediator.Send(new GetMyLastConnectedQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}/probe")]
        public async Task<IActionResult> ProbeConnection(Guid id)
        {
            var result = await _mediator.Send(new ProbeConnectionQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpPost("probe-batch")]
        public async Task<IActionResult> ProbeBatch([FromBody] IEnumerable<Guid> ids)
        {
            var result = await _mediator.Send(new ProbeBatchQuery(ids, HttpContext.User));
            return Ok(result);
        }

        [HttpPost("probe-bulk")]
        public async Task<IActionResult> ProbeConnectionsBulk()
        {
            var result = await _mediator.Send(new ProbeConnectionsBulkQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> DownloadConnectionFile(Guid id, [FromQuery] string? format)
        {
            var dto = await _mediator.Send(new DownloadConnectionFileQuery(id, format, HttpContext.User));
            return File(dto.Content, dto.ContentType, dto.FileName);
        }
    }
}
