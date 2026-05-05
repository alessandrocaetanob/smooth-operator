using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Users.Commands;
using SmoothOperator.Application.Features.Users.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var result = await _mediator.Send(new GetUsersQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var result = await _mediator.Send(new GetUserQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var result = await _mediator.Send(new GetRolesQuery(HttpContext.User));
            return Ok(result);
        }

        [HttpGet("{id}/vaults")]
        public async Task<IActionResult> GetVaultAssignments(Guid id)
        {
            var result = await _mediator.Send(new GetUserVaultAssignmentsQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest dto)
        {
            var result = await _mediator.Send(new UpdateUserCommand(id, dto, HttpContext.User));
            return Ok(result);
        }

        [HttpPut("{id}/role")]
        public async Task<IActionResult> SetRole(Guid id, [FromBody] SetUserRoleRequest dto)
        {
            var result = await _mediator.Send(new SetUserRoleCommand(id, dto, HttpContext.User));
            return Ok(result);
        }

        [HttpPut("{id}/vaults")]
        public async Task<IActionResult> SetVaultAssignments(Guid id, [FromBody] SetUserVaultAssignmentsRequest dto)
        {
            var result = await _mediator.Send(new SetUserVaultAssignmentsCommand(id, dto, HttpContext.User));
            return Ok(result);
        }

        [HttpPatch("{id}/active")]
        public async Task<IActionResult> SetActive(Guid id, [FromBody] SetUserActiveRequest dto)
        {
            var result = await _mediator.Send(new SetUserActiveCommand(id, dto, HttpContext.User));
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            await _mediator.Send(new DeleteUserCommand(id, HttpContext.User));
            return NoContent();
        }

        [AllowAnonymous]
        [HttpGet("{id}/effective-vaults")]
        public async Task<IActionResult> GetEffectiveVaults(Guid id)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            var result = await _mediator.Send(new GetEffectiveVaultsQuery(id, HttpContext.User));
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPut("me/profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            var result = await _mediator.Send(new UpdateMyProfileCommand(request, HttpContext.User));
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpDelete("me/avatar")]
        public async Task<IActionResult> DeleteMyAvatar()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            var result = await _mediator.Send(new DeleteMyAvatarCommand(HttpContext.User));
            return Ok(result);
        }
    }
}
