namespace SmoothOperator.Application.Features.AuditLogs.Queries
{
    /// <summary>
    /// Shared helper for building case-insensitive substring LIKE patterns used by
    /// the audit-log query handlers. Centralised so the escaping rules stay in one place.
    /// </summary>
    internal static class AuditLogLikePattern
    {
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
