using System;
using System.Security.Claims;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Infrastructure.Services
{
    public interface IAuditService
    {
        Task WriteAsync(string action, string resourceType, string? resourceId = null,
            object? details = null, string outcome = "success");
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly IAppMetrics _metrics;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AppDbContext context, IHttpContextAccessor http, IAppMetrics metrics,
            ILogger<AuditService> logger)
        {
            _context = context;
            _http = http;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task WriteAsync(string action, string resourceType, string? resourceId = null,
            object? details = null, string outcome = "success")
        {
            var ctx = _http.HttpContext;

            Guid? userId = null;
            var idClaim = ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idClaim, out var parsed))
                userId = parsed;

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
            await _context.SaveChangesAsync();

            _metrics.RecordAuditEvent(action);

            _logger.LogInformation(
                "Audit {action} on {resourceType}/{resourceId} outcome={outcome} userId={userId} ip={ipAddress} correlationId={correlationId}",
                action, resourceType, resourceId, outcome, userId, entry.IpAddress, entry.CorrelationId);
        }
    }
}
