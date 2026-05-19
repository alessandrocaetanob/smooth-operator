using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SmoothOperator.Application.Features.Connections
{
    /// <summary>
    /// Parses a connection's user-controlled <c>Settings</c> JSON object into a
    /// case-insensitive string dictionary. Centralised so the (lenient, fail-soft)
    /// parsing rules stay consistent between connection probing and session setup.
    /// </summary>
    public static class ConnectionSettingsParser
    {
        /// <summary>
        /// Parses <paramref name="settingsJson"/> into a settings dictionary.
        /// Returns an empty dictionary for null/blank/malformed input or a
        /// non-object root — malformed JSON is intentionally swallowed.
        /// </summary>
        public static Dictionary<string, string> Parse(string? settingsJson)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(settingsJson)) return dict;
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => string.Empty,
                        _ => prop.Value.ToString()
                    };
                }
            }
            catch
            {
                // Silently ignore malformed Settings — it's user-controlled JSON.
            }
            return dict;
        }
    }
}
