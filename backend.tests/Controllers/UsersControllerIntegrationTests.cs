using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests.Controllers;

public class UsersControllerIntegrationTests
{
    private static User MakeUser(string email, params string[] roles)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Name = email,
            IsActive = true,
            Roles = new List<Role>(),
            // Roles attached after seed when we have actual Role rows.
            CreatedAt = DateTime.UtcNow
        };
    }

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
    public async Task GetUsers_RequiresOwnerOrAdmin()
    {
        var ownerId = Guid.NewGuid();
        var regularId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "owner@x", Name = "owner", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
            var u = new User { Id = regularId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            db.Users.Add(u);
        });

        var asUser = AsUser(factory, regularId, AppRoles.User);
        var res = await asUser.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        var ok = await asOwner.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task SetRole_BlocksDemotingLastActiveOwner()
    {
        var ownerId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "only-owner@x", Name = "only", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
        }, seedPhantomOwner: false);

        var client = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await client.PutAsJsonAsync($"/api/users/{ownerId}/role", new SetUserRoleRequest { Role = AppRoles.Admin });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task SetRole_AllowsDemotionWhenOtherOwnersExist()
    {
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            foreach (var id in new[] { ownerAId, ownerBId })
            {
                var u = new User { Id = id, Email = $"o-{id:N}@x", Name = "o", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, u, AppRoles.Owner);
                db.Users.Add(u);
            }
        });

        var client = AsUser(factory, ownerAId, AppRoles.Owner);
        var res = await client.PutAsJsonAsync($"/api/users/{ownerBId}/role", new SetUserRoleRequest { Role = AppRoles.Admin });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var b = await db.Users.Include(u => u.Roles).FirstAsync(u => u.Id == ownerBId);
        Assert.Single(b.Roles);
        Assert.Equal(AppRoles.Admin, b.Roles.Single().Name);
    }

    [Fact]
    public async Task SetRole_RejectsUnknownRole()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "o@x", Name = "o", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
            var t = new User { Id = targetId, Email = "t@x", Name = "t", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, t, AppRoles.User);
            db.Users.Add(t);
        });

        var client = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await client.PutAsJsonAsync($"/api/users/{targetId}/role", new SetUserRoleRequest { Role = "Goblin" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_BlocksSelfDelete()
    {
        var ownerId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "self@x", Name = "self", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
        });

        var client = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await client.DeleteAsync($"/api/users/{ownerId}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_BlocksLastOwner()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "only@x", Name = "o", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        }, seedPhantomOwner: false);

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.DeleteAsync($"/api/users/{ownerId}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task SetVaultAssignments_RejectsUnknownVault()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "o@x", Name = "o", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
            var t = new User { Id = targetId, Email = "t@x", Name = "t", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, t, AppRoles.User);
            db.Users.Add(t);
        });

        var client = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await client.PutAsJsonAsync($"/api/users/{targetId}/vaults",
            new SetUserVaultAssignmentsRequest { VaultIds = new() { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetVaultAssignments_AssignsKnownVaults()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var vaultAId = Guid.NewGuid();
        var vaultBId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "o@x", Name = "o", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
            var t = new User { Id = targetId, Email = "t@x", Name = "t", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, t, AppRoles.User);
            db.Users.Add(t);
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultAId, Name = "vaultA" });
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultBId, Name = "vaultB" });
        });

        var client = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await client.PutAsJsonAsync($"/api/users/{targetId}/vaults",
            new SetUserVaultAssignmentsRequest { VaultIds = new() { vaultAId, vaultBId } });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t2 = await db.Users.Include(u => u.ConnectionGroups).FirstAsync(u => u.Id == targetId);
        Assert.Equal(2, t2.ConnectionGroups.Count);
    }
}
