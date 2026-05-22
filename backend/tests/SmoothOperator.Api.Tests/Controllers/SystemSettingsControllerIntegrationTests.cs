using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

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

        // 1. Get initial — all three fields default to 0
        var getInitialRes = await client.GetAsync("/api/settings/system");
        Assert.Equal(HttpStatusCode.OK, getInitialRes.StatusCode);
        var initial = await getInitialRes.Content.ReadFromJsonAsync<SystemSettingsDto>();
        Assert.NotNull(initial);
        Assert.Equal(0, initial.AuditLogRetentionDays);
        Assert.Equal(0, initial.IdleTimeoutMinutes);
        Assert.Equal(0, initial.MaxSessionMinutes);

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

    [Fact]
    public async Task Update_AcceptsIdleAndMaxSessionMinutes_ReturnsValues()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "sys-t@a", Name = "sys", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });
        var client = AsUser(factory, adminId, AppRoles.Admin);

        var updateReq = new UpdateSystemSettingsRequest
        {
            AuditLogRetentionDays = 90,
            IdleTimeoutMinutes = 15,
            MaxSessionMinutes = 480,
        };
        var updateRes = await client.PutAsJsonAsync("/api/settings/system", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<SystemSettingsDto>();
        Assert.NotNull(updated);
        Assert.Equal(15, updated.IdleTimeoutMinutes);
        Assert.Equal(480, updated.MaxSessionMinutes);

        // Re-fetch to confirm persistence
        var getRes = await client.GetAsync("/api/settings/system");
        var reloaded = await getRes.Content.ReadFromJsonAsync<SystemSettingsDto>();
        Assert.NotNull(reloaded);
        Assert.Equal(15, reloaded.IdleTimeoutMinutes);
        Assert.Equal(480, reloaded.MaxSessionMinutes);
    }

    [Fact]
    public async Task Update_NegativeIdleTimeout_Returns400()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "sys-neg@a", Name = "sys", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });
        var client = AsUser(factory, adminId, AppRoles.Admin);

        var res = await client.PutAsJsonAsync("/api/settings/system", new UpdateSystemSettingsRequest
        {
            AuditLogRetentionDays = 0,
            IdleTimeoutMinutes = -1,
            MaxSessionMinutes = 0,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Update_MaxSessionAbove10080_Returns400()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "sys-big@a", Name = "sys", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });
        var client = AsUser(factory, adminId, AppRoles.Admin);

        var res = await client.PutAsJsonAsync("/api/settings/system", new UpdateSystemSettingsRequest
        {
            AuditLogRetentionDays = 0,
            IdleTimeoutMinutes = 0,
            MaxSessionMinutes = 10081,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
