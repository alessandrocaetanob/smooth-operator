using System;
using System.Linq;
using SmoothOperator.Application.Exceptions;

namespace SmoothOperator.Application.Features.Webhooks
{
    /// <summary>
    /// Trims and validates admin-supplied webhook fields, throwing
    /// <see cref="BadRequestException"/> on bad input. Shared by the create and
    /// update handlers so both enforce the same rules.
    /// </summary>
    internal static class WebhookInputValidator
    {
        public static (string Name, string Url, string EventTypes) Normalize(
            string? name, string? url, string? eventTypes)
        {
            name = name?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                throw new BadRequestException("Webhook name is required.");
            if (name.Length > 100)
                throw new BadRequestException("Webhook name must be 100 characters or fewer.");

            url = url?.Trim() ?? string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Webhook URL must be an absolute HTTPS URL.");
            if (url.Length > 2048)
                throw new BadRequestException("Webhook URL must be 2048 characters or fewer.");

            return (name, url, NormalizeEventTypes(eventTypes));
        }

        private static string NormalizeEventTypes(string? raw)
        {
            var parts = (raw ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                throw new BadRequestException("Select at least one event type to subscribe to.");

            // "*" subsumes every other pattern — collapse to it.
            if (parts.Contains("*"))
                return "*";

            var joined = string.Join(",", parts.Distinct(StringComparer.OrdinalIgnoreCase));
            if (joined.Length > 1024)
                throw new BadRequestException("Event types must be 1024 characters or fewer.");
            return joined;
        }
    }
}
