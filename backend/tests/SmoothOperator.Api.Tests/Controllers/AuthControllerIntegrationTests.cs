using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class AuthControllerIntegrationTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
        {
            var role = db.Roles.First(r => r.Name == name);
            user.Roles.Add(role);
        }
    }

    [Fact]
    public async Task SetupStatus_NoUsers_RequiresSetup()
    {
        await using var factory = new TestWebApplicationFactory(seedPhantomOwner: false);
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/auth/setup-status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        
        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"requiresSetup\":true", content);
    }

    [Fact]
    public async Task SetupStatus_HasUsers_NoSetupRequired()
    {
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = Guid.NewGuid(), Email = "a@x", Name = "a" });
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/auth/setup-status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        
        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"requiresSetup\":false", content);
    }

    [Fact]
    public async Task Setup_WhenNoUsers_Succeeds()
    {
        await using var factory = new TestWebApplicationFactory(seedPhantomOwner: false);
        var client = factory.CreateClient();

        var req = new RegisterRequest { Email = "root@x", Name = "Root", Password = "Password123!!" };
        var res = await client.PostAsJsonAsync("/api/auth/setup", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var authRes = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authRes);
        Assert.NotNull(authRes.Token);
        Assert.Equal("root@x", authRes.User.Email);
    }

    [Fact]
    public async Task Setup_WhenUsersExist_Fails()
    {
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = Guid.NewGuid(), Email = "a@x", Name = "a" });
        });
        var client = factory.CreateClient();

        var req = new RegisterRequest { Email = "root@x", Name = "Root", Password = "Password123!!" };
        var res = await client.PostAsJsonAsync("/api/auth/setup", req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var password = "Password1!";
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = Guid.NewGuid(), Email = "login@x", Name = "login", IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4) };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });
        var client = factory.CreateClient();

        var req = new LoginRequest { Email = "login@x", Password = password };
        var res = await client.PostAsJsonAsync("/api/auth/login", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var authRes = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authRes);
        Assert.NotNull(authRes.Token);
        Assert.Equal("login@x", authRes.User.Email);
    }

    [Fact]
    public async Task Login_InvalidCredentials_Fails()
    {
        var password = "Password1!";
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = Guid.NewGuid(), Email = "login@x", Name = "login", IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4) };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });
        var client = factory.CreateClient();

        var req = new LoginRequest { Email = "login@x", Password = "wrongpassword" };
        var res = await client.PostAsJsonAsync("/api/auth/login", req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_InactiveUser_Fails()
    {
        var password = "Password1!";
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = Guid.NewGuid(), Email = "login@x", Name = "login", IsActive = false, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4) };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });
        var client = factory.CreateClient();

        var req = new LoginRequest { Email = "login@x", Password = password };
        var res = await client.PostAsJsonAsync("/api/auth/login", req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task InviteUser_ByAdmin_Succeeds()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", adminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.Admin);

        var req = new InviteUserRequest { Email = "invite@x", Name = "invite", Role = AppRoles.User };
        var res = await client.PostAsJsonAsync("/api/auth/invite", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task SmtpAvailable_ReturnsStatus()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/auth/smtp-available");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_Succeeds()
    {
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = Guid.NewGuid(), Email = "forgot@x", Name = "forgot", IsActive = true };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });
        var client = factory.CreateClient();

        var req = new ForgotPasswordRequest { Email = "forgot@x" };
        var res = await client.PostAsJsonAsync("/api/auth/forgot-password", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUserInfo_WhenAuthenticated()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "me@test.com", Name = "Me User", IsActive = true };
            AttachRoles(db, user, AppRoles.User, AppRoles.Admin);
            db.Users.Add(user);
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", $"{AppRoles.User},{AppRoles.Admin}");

        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var info = await res.Content.ReadFromJsonAsync<UserInfo>();
        Assert.NotNull(info);
        Assert.Equal("me@test.com", info!.Email);
        Assert.Contains(AppRoles.Admin, info.Roles);
        Assert.Contains(AppRoles.User, info.Roles);
    }

    [Fact]
    public async Task GetProviders_ReturnsConfiguredSso()
    {
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.SsoProviders.Add(new SsoProvider
            {
                Id = Guid.NewGuid(),
                Name = "Google",
                Type = SsoProviderType.Oidc,
                IsEnabled = true,
                EncryptedConfigJson = "encrypted-placeholder"
            });
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/auth/providers");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"sso\":true", content);
        Assert.Contains("\"ssoType\":\"Oidc\"", content);
        Assert.Contains("\"ssoName\":\"Google\"", content);
    }

    [Fact]
    public async Task InviteUser_Fails_WhenAssigningOwner()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", adminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.Admin);

        var req = new InviteUserRequest { Email = "owner-wanna-be@x", Name = "Wanna Be", Role = AppRoles.Owner };
        var res = await client.PostAsJsonAsync("/api/auth/invite", req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("Owner role cannot be assigned via invite", content);
    }

    [Fact]
    public async Task Register_ReturnsNotFound_WhenSelfRegisterDisabled()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.Admin);

        var req = new RegisterRequest { Email = "self@x", Name = "Self", Password = "Password123!!" };
        var res = await client.PostAsJsonAsync("/api/auth/register", req);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Register_Succeeds_WhenForcedEnabled()
    {
        await using var factory = new TestWebApplicationFactory();
        // PostConfigure runs after the factory's own PostConfigure (which sets AllowSelfRegister=false),
        // so this override wins reliably regardless of which appsettings.*.json files are loaded.
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<SmoothOperator.Application.Options.AuthOptions>(
                    o => o.AllowSelfRegister = true);
            });
        }).CreateClient();
        
        client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.Admin);

        var req = new RegisterRequest { Email = "self@test.com", Name = "Self", Password = "Password123!!" };
        var res = await client.PostAsJsonAsync("/api/auth/register", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        
        var authRes = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("self@test.com", authRes!.User.Email);
    }
}
