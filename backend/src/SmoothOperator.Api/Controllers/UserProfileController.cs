using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Users.Commands;
using SmoothOperator.Application.Features.Users.Queries;

namespace SmoothOperator.Api.Controllers
{
    /// <summary>
    /// Self-service user profile endpoints.
    /// Accessible by any authenticated user regardless of admin role.
    /// Kept separate from <see cref="UsersController"/> (admin-only) to avoid
    /// the [AllowAnonymous] + manual-auth-check antipattern that was previously
    /// required to bypass the class-level role restriction.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserProfileController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id}/effective-vaults")]
        public async Task<IActionResult> GetEffectiveVaults(Guid id)
        {
            var result = await _mediator.Send(new GetEffectiveVaultsQuery(id, HttpContext.User));
            return Ok(result);
        }

        [HttpPut("me/profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
        {
            var result = await _mediator.Send(new UpdateMyProfileCommand(request, HttpContext.User));
            return Ok(result);
        }

        [HttpDelete("me/avatar")]
        public async Task<IActionResult> DeleteMyAvatar()
        {
            var result = await _mediator.Send(new DeleteMyAvatarCommand(HttpContext.User));
            return Ok(result);
        }
    }
}
