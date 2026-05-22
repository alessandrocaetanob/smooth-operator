using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Webhooks
{
    /// <summary>
    /// Builds the JSON request body delivered to webhook endpoints. Centralised so
    /// real audit events and synthetic test pings share one schema.
    /// </summary>
    public static class WebhookPayloadFactory
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        /// <summary>Payload for a real audit event. <paramref name="deliveryId"/> is the queue row id.</summary>
        public static string BuildAuditEventPayload(Guid deliveryId, AuditLog entry)
        {
            // AuditLog.Details is stored as a JSON string — embed it as a nested
            // object rather than an escaped string. Fall back to a string value if
            // it is somehow not valid JSON.
            JsonNode? details;
            try
            {
                details = JsonNode.Parse(string.IsNullOrWhiteSpace(entry.Details) ? "{}" : entry.Details);
            }
            catch (JsonException)
            {
                details = JsonValue.Create(entry.Details);
            }

            var root = new JsonObject
            {
                ["id"] = deliveryId.ToString(),
                ["event"] = entry.Action,
                ["timestamp"] = entry.Timestamp.ToString("o"),
                ["data"] = new JsonObject
                {
                    ["auditLogId"] = entry.Id.ToString(),
                    ["action"] = entry.Action,
                    ["resourceType"] = entry.ResourceType,
                    ["resourceId"] = string.IsNullOrEmpty(entry.ResourceId) ? null : entry.ResourceId,
                    ["outcome"] = entry.Outcome,
                    ["userId"] = entry.UserId?.ToString(),
                    ["ipAddress"] = string.IsNullOrEmpty(entry.IpAddress) ? null : entry.IpAddress,
                    ["correlationId"] = entry.CorrelationId,
                    ["details"] = details,
                },
            };

            return root.ToJsonString(Options);
        }

        /// <summary>Payload for the "send test event" button — a synthetic <c>webhook.ping</c>.</summary>
        public static string BuildPingPayload(Guid deliveryId, WebhookEndpoint endpoint)
        {
            var root = new JsonObject
            {
                ["id"] = deliveryId.ToString(),
                ["event"] = "webhook.ping",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["data"] = new JsonObject
                {
                    ["webhookId"] = endpoint.Id.ToString(),
                    ["webhookName"] = endpoint.Name,
                    ["message"] = "This is a test event from Smooth Operator.",
                },
            };

            return root.ToJsonString(Options);
        }
    }
}
