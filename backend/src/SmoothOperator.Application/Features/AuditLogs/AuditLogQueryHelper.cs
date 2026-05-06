using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.AuditLogs
{
    public static class AuditLogQueryHelper
    {
        public static IQueryable<AuditLog> BuildFilteredQuery(
            IAppDbContext db,
            string? user,
            string? action,
            string? resourceType,
            DateTime? from,
            DateTime? to,
            string? outcome)
        {
            var q = db.AuditLogs.AsNoTracking().Include(l => l.User).AsQueryable();

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
    }
}
