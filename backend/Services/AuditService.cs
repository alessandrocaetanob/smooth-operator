using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Http;

namespace Backend.Services
{
    public interface IAuditService
    {
        Task WriteAsync(string action, string resourceType, string resourceId, object? details = null);
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _http;

        public AuditService(AppDbContext context, IHttpContextAccessor http)
        {
            _context = context;
            _http = http;
        }

        public async Task WriteAsync(string action, string resourceType, string resourceId, object? details = null)
        {
            Guid? userId = null;
            var idClaim = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idClaim, out var parsed))
            {
                userId = parsed;
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
                IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty
            };

            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync();
        }
    }
}
