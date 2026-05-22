using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Api.Tests.Webhooks;

public class WebhooksFlowTests
{
    private static void SeedUser(AppDbContext db, Guid userId, string role = "Owner")
    {
        var roleEntity = db.Roles.First(r => r.Name == role);
        var user = new User
        {
            Id = userId,
            Email = $"{role.ToLowerInvariant()}-webhook@example.com",
            Name = "Webhook Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        user.Roles.Add(roleEntity);
        db.Users.Add(user);
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, string role = "Owner")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static async Task<(Guid Id, string Secret)> CreateWebhookAsync(
        HttpClient client,
        string name = "siem",
        string url = "https://example.com/hook",
        string eventTypes = "*")
    {
        var res = await client.PostAsJsonAsync("/api/webhooks", new { name, url, eventTypes });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return (
            doc.RootElement.GetProperty("id").GetGuid(),
            doc.RootElement.GetProperty("secret").GetString()!);
    }

    [Fact]
    public async Task Create_ReturnsSecretOnce()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var (id, secret) = await CreateWebhookAsync(client);

        Assert.NotEqual(Guid.Empty, id);
        Assert.StartsWith("whsec_", secret);
    }

    [Fact]
    public async Task Create_NonHttpsUrl_Returns400()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var res = await client.PostAsJsonAsync("/api/webhooks", new
        {
            name = "insecure",
            url = "http://example.com/hook",
            eventTypes = "*",
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task List_DoesNotExposeSecret()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        await CreateWebhookAsync(client);

        var res = await client.GetAsync("/api/webhooks");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("whsec_", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, _) = await CreateWebhookAsync(client);

        var update = await client.PutAsJsonAsync($"/api/webhooks/{id}", new
        {
            name = "renamed",
            url = "https://example.com/v2",
            eventTypes = "connection.*",
            enabled = false,
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var endpoint = await db.WebhookEndpoints.SingleAsync(w => w.Id == id);
        Assert.Equal("renamed", endpoint.Name);
        Assert.Equal("https://example.com/v2", endpoint.Url);
        Assert.Equal("connection.*", endpoint.EventTypes);
        Assert.False(endpoint.Enabled);
    }

    [Fact]
    public async Task Delete_RemovesEndpoint()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, _) = await CreateWebhookAsync(client);

        var del = await client.DeleteAsync($"/api/webhooks/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.WebhookEndpoints.AnyAsync(w => w.Id == id));
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var res = await client.DeleteAsync($"/api/webhooks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_GetForbidden()
    {
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            SeedUser(db, ownerId);
            SeedUser(db, userId, "User");
        });
        var client = AsUser(factory, userId, "User");

        var res = await client.GetAsync("/api/webhooks");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task RotateSecret_ReturnsDifferentSecret()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, originalSecret) = await CreateWebhookAsync(client);

        var res = await client.PostAsync($"/api/webhooks/{id}/rotate-secret", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var rotated = doc.RootElement.GetProperty("secret").GetString()!;
        Assert.StartsWith("whsec_", rotated);
        Assert.NotEqual(originalSecret, rotated);
    }

    [Fact]
    public async Task SendTest_QueuesPingDelivery()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, _) = await CreateWebhookAsync(client);

        var res = await client.PostAsync($"/api/webhooks/{id}/test", null);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ping = await db.WebhookDeliveries
            .SingleAsync(d => d.WebhookEndpointId == id && d.EventType == "webhook.ping");
        Assert.Equal(WebhookDeliveryStatus.Pending, ping.Status);
        Assert.Contains("webhook.ping", ping.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditEvent_EnqueuesDelivery_ForMatchingSubscription()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        // A "*" endpoint receives every audit event — including its own webhook.created.
        var (id, _) = await CreateWebhookAsync(client, eventTypes: "*");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.WebhookDeliveries.AnyAsync(
            d => d.WebhookEndpointId == id && d.EventType == "webhook.created"));
    }

    [Fact]
    public async Task AuditEvent_DoesNotEnqueue_ForNonMatchingSubscription()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        // This endpoint only subscribes to connection.* — its webhook.created event
        // (and every other audited action here) must not produce a delivery.
        await CreateWebhookAsync(client, eventTypes: "connection.*");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.WebhookDeliveries.AnyAsync());
    }
}
