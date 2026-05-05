using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.SystemSettings.Commands;
using SmoothOperator.Application.Features.SystemSettings.Queries;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/settings/system")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class SystemSettingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SystemSettingsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<SystemSettingsDto>> Get()
        {
            var result = await _mediator.Send(new GetSystemSettingsQuery());
            return Ok(result);
        }

        [HttpPut]
        public async Task<ActionResult<SystemSettingsDto>> Update([FromBody] UpdateSystemSettingsRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _mediator.Send(new UpdateSystemSettingsCommand(request));
            return Ok(result);
        }
    }
}
