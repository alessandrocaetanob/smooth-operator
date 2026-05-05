using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.AuditLogs.Queries;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/audit-logs")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class AuditLogsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuditLogsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<PagedResult<AuditLogDto>>> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? user = null,
            [FromQuery] string? action = null,
            [FromQuery] string? resourceType = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? outcome = null)
        {
            var result = await _mediator.Send(new GetAuditLogsQuery(page, pageSize, user, action, resourceType, from, to, outcome));
            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? user = null,
            [FromQuery] string? action = null,
            [FromQuery] string? resourceType = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? outcome = null)
        {
            var bytes = await _mediator.Send(new ExportAuditLogsCsvQuery(user, action, resourceType, from, to, outcome));
            return File(bytes, "text/csv", "audit-logs.csv");
        }
    }
}
