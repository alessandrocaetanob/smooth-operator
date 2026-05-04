using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.Data;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/audit-logs")]
    [Authorize(Roles = AppRoles.OwnerOrAdmin)]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

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
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = BuildQuery(user, action, resourceType, from, to, outcome);

            var total = await q.CountAsync();
            var rows = await q
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AuditLogDto
                {
                    Id = l.Id,
                    Timestamp = l.Timestamp,
                    UserId = l.UserId,
                    UserEmail = l.User != null ? l.User.Email : null,
                    UserName = l.User != null ? l.User.Name : null,
                    Action = l.Action,
                    ResourceType = l.ResourceType,
                    ResourceId = l.ResourceId,
                    Details = l.Details,
                    IpAddress = l.IpAddress,
                    UserAgent = l.UserAgent,
                    CorrelationId = l.CorrelationId,
                    Outcome = l.Outcome
                })
                .ToListAsync();

            return Ok(new PagedResult<AuditLogDto>
            {
                Items = rows,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            });
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
            var rows = await BuildQuery(user, action, resourceType, from, to, outcome)
                .OrderByDescending(l => l.Timestamp)
                .Take(10000)
                .Select(l => new
                {
                    l.Timestamp,
                    UserEmail = l.User != null ? l.User.Email : "",
                    UserName = l.User != null ? l.User.Name : "",
                    l.Action,
                    l.ResourceType,
                    l.ResourceId,
                    l.IpAddress,
                    l.UserAgent,
                    l.CorrelationId,
                    l.Outcome,
                    l.Details
                })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("timestamp,user_email,user_name,action,resource_type,resource_id,ip_address,user_agent,correlation_id,outcome,details");
            foreach (var r in rows)
            {
                sb.Append(r.Timestamp.ToString("o", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(Csv(r.UserEmail)).Append(',');
                sb.Append(Csv(r.UserName)).Append(',');
                sb.Append(Csv(r.Action)).Append(',');
                sb.Append(Csv(r.ResourceType)).Append(',');
                sb.Append(Csv(r.ResourceId)).Append(',');
                sb.Append(Csv(r.IpAddress)).Append(',');
                sb.Append(Csv(r.UserAgent)).Append(',');
                sb.Append(Csv(r.CorrelationId)).Append(',');
                sb.Append(Csv(r.Outcome)).Append(',');
                sb.AppendLine(Csv(r.Details));
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "audit-logs.csv");
        }

        private IQueryable<Models.AuditLog> BuildQuery(string? user, string? action, string? resourceType, DateTime? from, DateTime? to, string? outcome = null)
        {
            var q = _context.AuditLogs.Include(l => l.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(user))
            {
                var term = user.Trim().ToLower();
                q = q.Where(l => l.User != null &&
                    (l.User.Email.ToLower().Contains(term) || l.User.Name.ToLower().Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                var term = action.Trim().ToLower();
                q = q.Where(l => l.Action.ToLower().Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                var term = resourceType.Trim();
                q = q.Where(l => l.ResourceType == term);
            }
            if (!string.IsNullOrWhiteSpace(outcome))
                q = q.Where(l => l.Outcome == outcome.Trim().ToLower());
            if (from.HasValue) q = q.Where(l => l.Timestamp >= from.Value);
            if (to.HasValue) q = q.Where(l => l.Timestamp <= to.Value);
            return q;
        }

        private static string Csv(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            // Prevent CSV Injection (Macro Execution)
            if (input.StartsWith("=") || input.StartsWith("+") || input.StartsWith("-") || input.StartsWith("@") || input.StartsWith("\t") || input.StartsWith("\r"))
            {
                input = "'" + input;
            }

            var needsQuote = input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r');
            var escaped = input.Replace("\"", "\"\"");
            return needsQuote ? $"\"{escaped}\"" : escaped;
        }
    }
}
