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
        public async Task<ActionResult<PagedResult<AuditLogDto>>> GetLogs([FromQuery] AuditLogFilterRequest filter)
        {
            var result = await _mediator.Send(new GetAuditLogsQuery(
                filter.Page, filter.PageSize, filter.User, filter.Action,
                filter.ResourceType, filter.From, filter.To, filter.Outcome));
            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] AuditLogFilterRequest filter)
        {
            var bytes = await _mediator.Send(new ExportAuditLogsCsvQuery(
                filter.User, filter.Action, filter.ResourceType, filter.From, filter.To, filter.Outcome));
            return File(bytes, "text/csv", "audit-logs.csv");
        }
    }
}
