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
}
