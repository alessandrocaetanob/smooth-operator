using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.SecretProviders.Commands;
using SmoothOperator.Application.Features.SecretProviders.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SecretProvidersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SecretProvidersController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var result = await _mediator.Send(new ListSecretProvidersQuery());
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _mediator.Send(new GetSecretProviderQuery(id));
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSecretProviderDto dto)
        {
            var result = await _mediator.Send(new CreateSecretProviderCommand(dto));
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSecretProviderDto dto)
        {
            var result = await _mediator.Send(new UpdateSecretProviderCommand(id, dto));
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(new DeleteSecretProviderCommand(id));
            return NoContent();
        }

        [HttpPost("{id:guid}/test")]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            var result = await _mediator.Send(new TestSecretProviderConnectionCommand(id));
            return Ok(new { success = result });
        }

        [HttpGet("{id:guid}/secrets")]
        public async Task<IActionResult> ListSecrets(Guid id)
        {
            var result = await _mediator.Send(new ListSecretNamesQuery(id));
            return Ok(result);
        }
    }
}
