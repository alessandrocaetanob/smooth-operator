using System;
using System.Text.Json;
using FluentAssertions;
using SmoothOperator.Application.Features.Webhooks;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Tests.Features.Webhooks;

public sealed class WebhookPayloadFactoryTests
{
    private static AuditLog SampleAudit(string details = "{\"foo\":\"bar\"}") => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Timestamp = new DateTime(2026, 5, 22, 20, 0, 0, DateTimeKind.Utc),
        UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Action = "connection.started",
        ResourceType = "Connection",
        ResourceId = "abc",
        Outcome = "success",
        IpAddress = "203.0.113.7",
        CorrelationId = "corr-1",
        Details = details,
    };

    [Fact]
    public void BuildAuditEventPayload_HasAllExpectedFields()
    {
        var deliveryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var json = WebhookPayloadFactory.BuildAuditEventPayload(deliveryId, SampleAudit());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be(deliveryId.ToString());
        root.GetProperty("event").GetString().Should().Be("connection.started");
        root.GetProperty("timestamp").GetString().Should().StartWith("2026-05-22T20:00:00");

        var data = root.GetProperty("data");
        data.GetProperty("auditLogId").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
        data.GetProperty("action").GetString().Should().Be("connection.started");
        data.GetProperty("resourceType").GetString().Should().Be("Connection");
        data.GetProperty("resourceId").GetString().Should().Be("abc");
        data.GetProperty("outcome").GetString().Should().Be("success");
        data.GetProperty("userId").GetString().Should().Be("22222222-2222-2222-2222-222222222222");
        data.GetProperty("ipAddress").GetString().Should().Be("203.0.113.7");
        data.GetProperty("correlationId").GetString().Should().Be("corr-1");
        data.GetProperty("details").GetProperty("foo").GetString().Should().Be("bar");
    }

    [Fact]
    public void BuildAuditEventPayload_NullishOptionalFields_RenderAsJsonNull()
    {
        var audit = SampleAudit();
        audit.UserId = null;
        audit.ResourceId = string.Empty;
        audit.IpAddress = string.Empty;
        audit.CorrelationId = null;

        var json = WebhookPayloadFactory.BuildAuditEventPayload(Guid.NewGuid(), audit);
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        data.GetProperty("userId").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("resourceId").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("ipAddress").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("correlationId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void BuildAuditEventPayload_WhitespaceDetails_RenderAsEmptyObject()
    {
        var json = WebhookPayloadFactory.BuildAuditEventPayload(Guid.NewGuid(), SampleAudit(details: "   "));
        using var doc = JsonDocument.Parse(json);
        var details = doc.RootElement.GetProperty("data").GetProperty("details");
        details.ValueKind.Should().Be(JsonValueKind.Object);
        details.EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void BuildAuditEventPayload_NonJsonDetails_FallBackToStringValue()
    {
        var json = WebhookPayloadFactory.BuildAuditEventPayload(Guid.NewGuid(), SampleAudit(details: "not json!"));
        using var doc = JsonDocument.Parse(json);
        var details = doc.RootElement.GetProperty("data").GetProperty("details");
        details.ValueKind.Should().Be(JsonValueKind.String);
        details.GetString().Should().Be("not json!");
    }

    [Fact]
    public void BuildPingPayload_HasExpectedShape()
    {
        var endpoint = new WebhookEndpoint
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "siem",
            Url = "https://example.com/hook",
            EncryptedSecret = "enc",
            EventTypes = "*",
            Enabled = true,
        };
        var deliveryId = Guid.NewGuid();

        var json = WebhookPayloadFactory.BuildPingPayload(deliveryId, endpoint);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be(deliveryId.ToString());
        root.GetProperty("event").GetString().Should().Be("webhook.ping");
        root.GetProperty("timestamp").GetString().Should().NotBeNullOrWhiteSpace();
        var data = root.GetProperty("data");
        data.GetProperty("webhookId").GetString().Should().Be(endpoint.Id.ToString());
        data.GetProperty("webhookName").GetString().Should().Be("siem");
        data.GetProperty("message").GetString().Should().Contain("test event");
    }
}
