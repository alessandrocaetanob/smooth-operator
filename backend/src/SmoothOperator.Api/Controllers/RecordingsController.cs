using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Recordings.Commands;
using SmoothOperator.Application.Features.Recordings.Queries;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/recordings")]
    [Authorize]
    public class RecordingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RecordingsController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Paginated, access-scoped list of recordings. Non-admins only see recordings
        /// for connections they can use; admins/owners see everything.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<RecordingsListDto>> List(
            [FromQuery] Guid? userId,
            [FromQuery] Guid? connectionId,
            [FromQuery] Guid? vaultId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] RecordingStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(
                new GetRecordingsQuery(User, userId, connectionId, vaultId, from, to, status, page, pageSize),
                cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Streams the raw <c>.guac</c> playback bytes. The frontend pipes this into
        /// <c>Guacamole.SessionRecording</c> from <c>guacamole-common-js</c>. Returns 404
        /// (not 403) when the caller lacks access so existence isn't leaked.
        /// </summary>
        [HttpGet("{id:guid}/stream")]
        public async Task<IActionResult> Stream(Guid id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetRecordingStreamQuery(User, id), cancellationToken);
            return File(result.Content, "application/octet-stream", result.FileName, enableRangeProcessing: true);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoles.OwnerOrAdmin)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteRecordingCommand(id), cancellationToken);
            return NoContent();
        }
    }
}
