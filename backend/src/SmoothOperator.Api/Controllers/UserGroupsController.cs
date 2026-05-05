using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.UserGroups.Commands;
using SmoothOperator.Application.Features.UserGroups.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/groups")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class UserGroupsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserGroupsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var result = await _mediator.Send(new GetUserGroupsQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _mediator.Send(new GetUserGroupQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}/vaults")]
        public async Task<IActionResult> ListVaults(Guid id)
        {
            var result = await _mediator.Send(new GetUserGroupVaultsQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserGroupRequest req)
        {
            var result = await _mediator.Send(new CreateUserGroupCommand(req, HttpContext.User));
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RenameUserGroupRequest req)
        {
            var result = await _mediator.Send(new UpdateUserGroupCommand(id, req, HttpContext.User));
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(new DeleteUserGroupCommand(id, HttpContext.User));
            return NoContent();
        }

        [HttpPut("{id}/members")]
        public async Task<IActionResult> SetMembers(Guid id, [FromBody] SetUserGroupMembersRequest req)
        {
            await _mediator.Send(new SetUserGroupMembersCommand(id, req, HttpContext.User));
            return NoContent();
        }
    }
}
