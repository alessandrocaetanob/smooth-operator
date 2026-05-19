using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.AuditLogs.Queries
{
    /// <summary>
    /// Shared query-building helpers for the audit-log handlers. Centralised so the
    /// filter logic and LIKE-escaping rules stay in one place (used by both
    /// <see cref="GetAuditLogsQuery"/> and <see cref="ExportAuditLogsCsvQuery"/>).
    /// </summary>
    internal static class AuditLogQueryFilters
    {
        /// <summary>
        /// Builds the base audit-log query with the supplied filters applied.
        /// </summary>
        public static IQueryable<AuditLog> ApplyFilters(
            IAppDbContext db,
            string? user, string? action, string? resourceType,
            DateTime? from, DateTime? to, string? outcome)
        {
            var q = db.AuditLogs.AsNoTracking().Include(l => l.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(user))
            {
                // LOWER(col) LIKE '%term%' is backed by the expression GIN trigram
                // indexes added in the AddTrigramSearchIndexes migration.
                var pattern = ToLikePattern(user.Trim().ToLower());
                q = q.Where(l => l.User != null &&
                    (EF.Functions.Like(l.User.Email.ToLower(), pattern, "\\")
                        || EF.Functions.Like(l.User.Name.ToLower(), pattern, "\\")));
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                var pattern = ToLikePattern(action.Trim().ToLower());
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

        /// <summary>
        /// Escapes LIKE metacharacters so the caller's term is matched literally,
        /// then wraps it for case-insensitive substring matching.
        /// </summary>
        public static string ToLikePattern(string term)
        {
            var escaped = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            return $"%{escaped}%";
        }
    }
}
