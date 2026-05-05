using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.ConnectionGroups.Commands;
using SmoothOperator.Application.Features.ConnectionGroups.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/vaults")]
    [Authorize]
    public class ConnectionGroupsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ConnectionGroupsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetVaults()
        {
            var result = await _mediator.Send(new GetVaultsQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> CreateVault([FromBody] CreateConnectionGroupDto dto)
        {
            var result = await _mediator.Send(new CreateVaultCommand(dto, HttpContext.User));
            return CreatedAtAction(nameof(GetVaults), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> UpdateVault(Guid id, [FromBody] CreateConnectionGroupDto dto)
        {
            await _mediator.Send(new UpdateVaultCommand(id, dto, HttpContext.User));
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> DeleteVault(Guid id)
        {
            await _mediator.Send(new DeleteVaultCommand(id, HttpContext.User));
            return NoContent();
        }

        [HttpGet("{id}/assignments")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> GetAssignments(Guid id)
        {
            var result = await _mediator.Send(new GetVaultAssignmentsQuery(id));
            return Ok(result);
        }

        [HttpPut("{id}/assignments")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> SetAssignments(Guid id, [FromBody] VaultAssignmentsDto dto)
        {
            await _mediator.Send(new SetVaultAssignmentsCommand(id, dto, HttpContext.User));
            return NoContent();
        }

        [HttpGet("{id}/effective-users")]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<IActionResult> GetEffectiveUsers(Guid id)
        {
            var result = await _mediator.Send(new GetEffectiveUsersQuery(id, HttpContext.User));
            return Ok(result);
        }
    }
}
