using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.RecordingStorageSettings.Commands;
using SmoothOperator.Application.Features.RecordingStorageSettings.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/settings/recording-storage")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class RecordingStorageSettingsController : ControllerBase
    {
        private const string CacheTag = "recording-storage";

        private readonly IMediator _mediator;
        private readonly IOutputCacheStore _cacheStore;

        public RecordingStorageSettingsController(IMediator mediator, IOutputCacheStore cacheStore)
        {
            _mediator = mediator;
            _cacheStore = cacheStore;
        }

        [HttpGet]
        [OutputCache(PolicyName = "ShortCache", Tags = [CacheTag])]
        public async Task<ActionResult<RecordingStorageSettingsDto>> Get()
        {
            var result = await _mediator.Send(new GetRecordingStorageSettingsQuery());
            return Ok(result);
        }

        [HttpPut]
        public async Task<ActionResult<RecordingStorageSettingsDto>> Update(
            [FromBody] UpdateRecordingStorageSettingsRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _mediator.Send(new SaveRecordingStorageSettingsCommand(request), cancellationToken);
            await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
            return Ok(result);
        }

        [HttpPost("test")]
        public async Task<ActionResult<RecordingStorageTestResult>> Test(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new TestRecordingStorageCommand(), cancellationToken);
            if (!result.Success)
                return StatusCode(502, result);
            return Ok(result);
        }
    }
}
