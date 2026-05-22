using System;
using System.Security.Claims;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly IAppMetrics _metrics;
        private readonly IWebhookEnqueuer _webhookEnqueuer;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AppDbContext context, IHttpContextAccessor http, IAppMetrics metrics,
            IWebhookEnqueuer webhookEnqueuer, ILogger<AuditService> logger)
        {
            _context = context;
            _http = http;
            _metrics = metrics;
            _webhookEnqueuer = webhookEnqueuer;
            _logger = logger;
        }

        public async Task WriteAsync(string action, string resourceType, string? resourceId = null,
            object? details = null, string outcome = "success")
        {
            var ctx = _http.HttpContext;

            Guid? userId = null;
            var idClaim = ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idClaim, out var parsed))
            {
                // Verify the user still exists. A stale JWT (e.g. after a DB reset
                // where the browser still holds a token from the prior data) would
                // otherwise produce an FK violation on insert and crash the request.
                var exists = await _context.Users.AnyAsync(u => u.Id == parsed);
                if (exists) userId = parsed;
            }

            var entry = new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId ?? string.Empty,
                Details = details == null ? "{}" : System.Text.Json.JsonSerializer.Serialize(details),
                IpAddress = ctx?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserAgent = ctx?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null,
                CorrelationId = ctx?.Items["CorrelationId"] as string,
                Outcome = outcome
            };

            _context.AuditLogs.Add(entry);

            // Fan the event out to subscribed webhook endpoints as queued delivery
            // rows. They are committed by the SaveChangesAsync below, in the same
            // transaction as the audit log (transactional outbox). A fault here must
            // never fail the audit write, so it is logged and swallowed.
            try
            {
                await _webhookEnqueuer.EnqueueAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook enqueue failed for audit action {Action}", action);
            }

            await _context.SaveChangesAsync();

            _metrics.RecordAuditEvent(action);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "Audit {Action} on {ResourceType}/{ResourceId} outcome={Outcome} userId={UserId} ip={IpAddress} correlationId={CorrelationId}",
                    action, resourceType, resourceId, outcome, userId, entry.IpAddress, entry.CorrelationId);
        }
    }
}
