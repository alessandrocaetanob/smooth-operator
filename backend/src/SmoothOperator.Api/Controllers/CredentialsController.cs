using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Credentials.Commands;
using SmoothOperator.Application.Features.Credentials.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
    public class CredentialsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CredentialsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetCredentials()
        {
            var result = await _mediator.Send(new GetCredentialsQuery(null, HttpContext.User));
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> CreateCredential([FromBody] CreateCredentialDto dto)
        {
            var result = await _mediator.Send(new CreateCredentialCommand(dto, HttpContext.User));
            return CreatedAtAction(nameof(GetCredentials), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> UpdateCredential(Guid id, [FromBody] CreateCredentialDto dto)
        {
            await _mediator.Send(new UpdateCredentialCommand(id, dto, HttpContext.User));
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> DeleteCredential(Guid id)
        {
            await _mediator.Send(new DeleteCredentialCommand(id, HttpContext.User));
            return NoContent();
        }

        [HttpPost("generate-ssh")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> GenerateSshKey([FromBody] GenerateSshKeyRequest request)
        {
            var result = await _mediator.Send(new GenerateSshKeyQuery(request.KeyType));
            return Ok(result);
        }
    }
}
