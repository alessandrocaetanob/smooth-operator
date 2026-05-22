using System;
using System.Linq;

namespace SmoothOperator.Application.Features.Webhooks
{
    /// <summary>
    /// Matches an audit action string against an endpoint's comma-separated
    /// subscription patterns. A pattern is one of:
    /// <list type="bullet">
    ///   <item><c>*</c> — every event.</item>
    ///   <item><c>prefix.*</c> — category match (e.g. <c>user.*</c> matches <c>user.login_success</c>).</item>
    ///   <item>any other value — an exact, case-insensitive action match.</item>
    /// </list>
    /// </summary>
    public static class WebhookEventMatcher
    {
        private static readonly char[] Separator = [','];

        /// <summary>True when any pattern in <paramref name="patterns"/> matches <paramref name="action"/>.</summary>
        public static bool Matches(string? patterns, string action)
        {
            if (string.IsNullOrWhiteSpace(patterns) || string.IsNullOrWhiteSpace(action))
                return false;

            return patterns
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(pattern => MatchesSingle(pattern, action));
        }

        private static bool MatchesSingle(string pattern, string action)
        {
            if (pattern == "*")
                return true;

            if (pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                // Keep the trailing dot so "user.*" cannot match "userspace.x".
                var prefix = pattern[..^1];
                return action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(pattern, action, StringComparison.OrdinalIgnoreCase);
        }
    }
}
