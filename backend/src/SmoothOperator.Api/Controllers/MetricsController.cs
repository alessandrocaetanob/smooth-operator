using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.Metrics.Queries;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/metrics")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class MetricsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MetricsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("summary")]
        public async Task<ActionResult<MetricsSummaryDto>> GetSummary()
        {
            var result = await _mediator.Send(new GetMetricsSummaryQuery());
            return Ok(result);
        }

        [HttpGet("logins/timeseries")]
        public async Task<ActionResult<IEnumerable<TimeseriesBucketDto>>> GetLoginTimeseries(
            [FromQuery] int hours = 1,
            [FromQuery] int bucketMinutes = 5)
        {
            var result = await _mediator.Send(new GetLoginTimeseriesQuery(hours, bucketMinutes));
            return Ok(result);
        }

        [HttpGet("connections/timeseries")]
        public async Task<ActionResult<IEnumerable<TimeseriesBucketDto>>> GetConnectionsTimeseries(
            [FromQuery] int hours = 24,
            [FromQuery] int bucketMinutes = 60)
        {
            var result = await _mediator.Send(new GetConnectionsTimeseriesQuery(hours, bucketMinutes));
            return Ok(result);
        }

        [HttpGet("audit-events/top")]
        public async Task<ActionResult<IEnumerable<TopEventDto>>> GetTopAuditEvents(
            [FromQuery] int hours = 24,
            [FromQuery] int limit = 10)
        {
            var result = await _mediator.Send(new GetTopAuditEventsQuery(hours, limit));
            return Ok(result);
        }

        [HttpGet("audit-events/breakdown")]
        public async Task<ActionResult<OutcomeBreakdownDto>> GetAuditEventBreakdown(
            [FromQuery] int hours = 24)
        {
            var result = await _mediator.Send(new GetAuditEventBreakdownQuery(hours));
            return Ok(result);
        }

        [HttpGet("audit-events/timeseries")]
        public async Task<ActionResult<IEnumerable<TimeseriesBucketDto>>> GetAuditEventTimeseries(
            [FromQuery] int hours = 24,
            [FromQuery] int bucketMinutes = 60)
        {
            var result = await _mediator.Send(new GetAuditEventTimeseriesQuery(hours, bucketMinutes));
            return Ok(result);
        }
    }
}
