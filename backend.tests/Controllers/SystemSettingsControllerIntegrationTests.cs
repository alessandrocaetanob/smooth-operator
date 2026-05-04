using System.Net;
using System.Net.Http.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Tests.Infrastructure;
using Xunit;

namespace Backend.Tests.Controllers;

public class SystemSettingsControllerIntegrationTests
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
    public async Task Get_AndUpdate_SystemSettings()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "sys@a", Name = "sys", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Get initial
        var getInitialRes = await client.GetAsync("/api/settings/system");
        Assert.Equal(HttpStatusCode.OK, getInitialRes.StatusCode);
        var initial = await getInitialRes.Content.ReadFromJsonAsync<SystemSettingsDto>();
        Assert.NotNull(initial);
        Assert.Equal(0, initial.AuditLogRetentionDays);

        // 2. Update
        var updateReq = new UpdateSystemSettingsRequest { AuditLogRetentionDays = 30 };
        var updateRes = await client.PutAsJsonAsync("/api/settings/system", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<SystemSettingsDto>();
        Assert.NotNull(updated);
        Assert.Equal(30, updated.AuditLogRetentionDays);

        // 3. Get again
        var getAgainRes = await client.GetAsync("/api/settings/system");
        Assert.Equal(HttpStatusCode.OK, getAgainRes.StatusCode);
        var final = await getAgainRes.Content.ReadFromJsonAsync<SystemSettingsDto>();
        Assert.NotNull(final);
        Assert.Equal(30, final.AuditLogRetentionDays);
    }
}
