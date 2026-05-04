using System.Net;
using System.Net.Http.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests.Controllers;

public class SmtpSettingsControllerIntegrationTests
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
    public async Task Get_Update_And_Test_SmtpSettings()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Get initial
        var getInitialRes = await client.GetAsync("/api/settings/smtp");
        Assert.Equal(HttpStatusCode.OK, getInitialRes.StatusCode);
        var initial = await getInitialRes.Content.ReadFromJsonAsync<SmtpSettingsDto>();
        Assert.NotNull(initial);
        Assert.False(initial.Configured);

        // 2. Update
        var updateReq = new UpdateSmtpSettingsRequest
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = true,
            Username = "user",
            Password = "password",
            FromAddress = "noreply@example.com",
            FromName = "Smooth Operator",
            Enabled = true
        };
        var updateRes = await client.PutAsJsonAsync("/api/settings/smtp", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<SmtpSettingsDto>();
        Assert.NotNull(updated);
        Assert.True(updated.Configured);
        Assert.Equal("smtp.example.com", updated.Host);
        Assert.True(updated.HasPassword);

        // 3. Test - expecting failure since SMTP is not actually running
        var testReq = new SmtpTestRequest { ToEmail = "test@example.com" };
        var testRes = await client.PostAsJsonAsync("/api/settings/smtp/test", testReq);
        Assert.Equal(HttpStatusCode.BadGateway, testRes.StatusCode); // 502
    }
}
