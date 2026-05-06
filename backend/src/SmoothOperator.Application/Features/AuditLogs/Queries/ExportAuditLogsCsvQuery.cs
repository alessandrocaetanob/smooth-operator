using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.AuditLogs.Queries
{
    public sealed record ExportAuditLogsCsvQuery(
        string? User = null,
        string? Action = null,
        string? ResourceType = null,
        DateTime? From = null,
        DateTime? To = null,
        string? Outcome = null) : IRequest<byte[]>;

    public sealed class ExportAuditLogsCsvQueryHandler : IRequestHandler<ExportAuditLogsCsvQuery, byte[]>
    {
        private readonly IAppDbContext _db;

        public ExportAuditLogsCsvQueryHandler(IAppDbContext db) => _db = db;

        public async Task<byte[]> Handle(ExportAuditLogsCsvQuery request, CancellationToken cancellationToken)
        {
            var rows = await AuditLogQueryHelper.BuildFilteredQuery(_db, request.User, request.Action, request.ResourceType, request.From, request.To, request.Outcome)
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
                .ToListAsync(cancellationToken);

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

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string Csv(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            // Prevent CSV Injection (Formula Injection)
            var trimmed = input.TrimStart(' ', '\t', '\r', '\n');
            if (trimmed.Length > 0 &&
                (trimmed[0] == '=' || trimmed[0] == '+' || trimmed[0] == '-' || trimmed[0] == '@'))
            {
                input = "'" + input;
            }

            var needsQuote = input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r');
            var escaped = input.Replace("\"", "\"\"");
            return needsQuote ? $"\"{escaped}\"" : escaped;
        }
    }
}
