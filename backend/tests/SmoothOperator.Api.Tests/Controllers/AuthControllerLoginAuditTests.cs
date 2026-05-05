using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class AuthControllerLoginAuditTests
{
    [Fact]
    public async Task Login_WithUnknownUser_WritesFailedAuditEvent()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing@test.local",
            password = "WrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.Action == "user.login_failed" &&
            a.Outcome == "failure"));
    }

    [Fact]
    public async Task Login_WithDisabledUser_WritesFailedAuditEvent()
    {
        var disabledUserId = Guid.NewGuid();
        using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User
            {
                Id = disabledUserId,
                Email = "disabled@test.local",
                Name = "disabled-user",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!", workFactor: 12),
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "disabled@test.local",
            password = "CorrectPassword123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.Action == "user.login_failed" &&
            a.ResourceId == disabledUserId.ToString() &&
            a.Outcome == "failure"));
    }
}
