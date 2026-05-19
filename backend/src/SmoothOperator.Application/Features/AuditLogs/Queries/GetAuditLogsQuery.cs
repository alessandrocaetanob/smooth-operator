using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.AuditLogs.Queries
{
    public sealed record GetAuditLogsQuery(
        int Page = 1,
        int PageSize = 25,
        string? User = null,
        string? Action = null,
        string? ResourceType = null,
        DateTime? From = null,
        DateTime? To = null,
        string? Outcome = null) : IRequest<PagedResult<AuditLogDto>>;

    public sealed class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
    {
        private readonly IAppDbContext _db;

        public GetAuditLogsQueryHandler(IAppDbContext db) => _db = db;

        public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 200);

            var q = BuildQuery(request.User, request.Action, request.ResourceType, request.From, request.To, request.Outcome);

            var total = await q.CountAsync(cancellationToken);
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
                .ToListAsync(cancellationToken);

            return new PagedResult<AuditLogDto>
            {
                Items = rows,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };
        }

        private IQueryable<AuditLog> BuildQuery(
            string? user, string? action, string? resourceType,
            DateTime? from, DateTime? to, string? outcome)
        {
            var q = _db.AuditLogs.AsNoTracking().Include(l => l.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(user))
            {
                // LOWER(col) LIKE '%term%' is backed by the expression GIN trigram
                // indexes added in the AddTrigramSearchIndexes migration.
                var pattern = AuditLogLikePattern.ToLikePattern(user.Trim().ToLower());
                q = q.Where(l => l.User != null &&
                    (EF.Functions.Like(l.User.Email.ToLower(), pattern, "\\")
                        || EF.Functions.Like(l.User.Name.ToLower(), pattern, "\\")));
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                var pattern = AuditLogLikePattern.ToLikePattern(action.Trim().ToLower());
                q = q.Where(l => EF.Functions.Like(l.Action.ToLower(), pattern, "\\"));
            }
            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                var term = resourceType.Trim();
                q = q.Where(l => l.ResourceType == term);
            }
            if (!string.IsNullOrWhiteSpace(outcome))
            {
                var outcomeLower = outcome.Trim().ToLower();
                q = q.Where(l => l.Outcome == outcomeLower);
            }
            if (from.HasValue) q = q.Where(l => l.Timestamp >= from.Value);
            if (to.HasValue) q = q.Where(l => l.Timestamp <= to.Value);
            return q;
        }
    }
}
