using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class AuditLogsControllerIntegrationTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
        {
            var role = db.Roles.First(r => r.Name == name);
            user.Roles.Add(role);
        }
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    [Fact]
    public async Task GetLogs_And_Export()
    {
        var adminId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "auditor@a", Name = "auditor", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.AuditLogs.Add(new AuditLog
            {
                Id = logId,
                Timestamp = DateTime.UtcNow,
                UserId = adminId,
                Action = "test.action",
                ResourceType = "TestResource",
                ResourceId = "test-123",
                Outcome = "success",
                Details = "{ \"k\": \"v\" }"
            });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Get Logs
        var getRes = await client.GetAsync("/api/audit-logs?action=test.action&resourceType=TestResource");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var paged = await getRes.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("test.action", paged.Items.First().Action);
        Assert.Equal("test-123", paged.Items.First().ResourceId);

        // 2. Export
        var exportRes = await client.GetAsync("/api/audit-logs/export?outcome=success");
        Assert.Equal(HttpStatusCode.OK, exportRes.StatusCode);
        var csv = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains("timestamp,user_email,user_name,action,resource_type,resource_id,ip_address,user_agent,correlation_id,outcome,details", csv);
        Assert.Contains("test.action", csv);
        Assert.Contains("TestResource", csv);
        Assert.Contains("{ \"\"k\"\": \"\"v\"\" }", csv); // escaped quotes in CSV
    }

    [Fact]
    public async Task GetLogs_FilterByUser_MatchesEmailOrNameCaseInsensitively()
    {
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var alice = new User { Id = aliceId, Email = "alice@corp.test", Name = "Alice Auditor", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, alice, AppRoles.Admin);
            db.Users.Add(alice);
            db.Users.Add(new User { Id = bobId, Email = "bob@corp.test", Name = "Bob Builder", IsActive = true, CreatedAt = DateTime.UtcNow });

            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, UserId = aliceId, Action = "alpha.event", ResourceType = "R", Outcome = "success" });
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, UserId = bobId, Action = "beta.event", ResourceType = "R", Outcome = "success" });
        });

        var client = AsUser(factory, aliceId, AppRoles.Admin);

        // Match a substring of the display name, in a different case.
        var byName = await client.GetAsync("/api/audit-logs?user=ALICE");
        Assert.Equal(HttpStatusCode.OK, byName.StatusCode);
        var named = await byName.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        Assert.NotNull(named);
        Assert.Single(named.Items);
        Assert.Equal("alpha.event", named.Items.First().Action);

        // Match a substring of the email address.
        var byEmail = await client.GetAsync("/api/audit-logs?user=bob@corp");
        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
        var emailed = await byEmail.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        Assert.NotNull(emailed);
        Assert.Single(emailed.Items);
        Assert.Equal("beta.event", emailed.Items.First().Action);
    }

    [Fact]
    public async Task GetLogs_FilterByDateRange_ReturnsOnlyLogsInsideWindow()
    {
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "auditor@a", Name = "auditor", IsActive = true, CreatedAt = now };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Timestamp = now.AddDays(-2), UserId = adminId, Action = "old.event", ResourceType = "R", Outcome = "success" });
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Timestamp = now.AddDays(-1), UserId = adminId, Action = "mid.event", ResourceType = "R", Outcome = "success" });
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Timestamp = now, UserId = adminId, Action = "recent.event", ResourceType = "R", Outcome = "success" });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // A window centred on the mid log excludes both the old and recent logs,
        // exercising both the `from` and `to` filter branches.
        var from = Uri.EscapeDataString(now.AddDays(-1).AddHours(-12).ToString("o"));
        var to = Uri.EscapeDataString(now.AddDays(-1).AddHours(12).ToString("o"));

        var res = await client.GetAsync($"/api/audit-logs?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var paged = await res.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("mid.event", paged.Items.First().Action);
    }
}
