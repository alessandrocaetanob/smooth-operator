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

public class ConnectionGroupsControllerTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
        {
            user.Roles.Add(db.Roles.First(r => r.Name == name));
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
    public async Task GetVaults_ReturnsAllVaults_ForOwner()
    {
        var ownerId = Guid.NewGuid();
        var vault1Id = Guid.NewGuid();
        var vault2Id = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vault1Id, Name = "Vault A" });
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vault2Id, Name = "Vault B" });

            var owner = new User { Id = ownerId, Email = "owner@x", Name = "owner", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
        });

        var client = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await client.GetAsync("/api/vaults");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var vaults = await res.Content.ReadFromJsonAsync<List<ConnectionGroupDto>>();
        Assert.NotNull(vaults);
        Assert.Equal(2, vaults.Count);
        Assert.Contains(vaults, v => v.Id == vault1Id && v.Name == "Vault A");
        Assert.Contains(vaults, v => v.Id == vault2Id && v.Name == "Vault B");
    }

    [Fact]
    public async Task GetVaults_ReturnsAllVaults_ForAdmin()
    {
        var adminId = Guid.NewGuid();
        var vault1Id = Guid.NewGuid();
        var vault2Id = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vault1Id, Name = "Vault A" });
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vault2Id, Name = "Vault B" });

            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.GetAsync("/api/vaults");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var vaults = await res.Content.ReadFromJsonAsync<List<ConnectionGroupDto>>();
        Assert.NotNull(vaults);
        Assert.Equal(2, vaults.Count);
    }

    [Fact]
    public async Task GetVaults_ReturnsOnlyAssignedVaults_ForUser()
    {
        var userId = Guid.NewGuid();
        var assignedVaultId = Guid.NewGuid();
        var unassignedVaultId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var assigned = new ConnectionGroup { Id = assignedVaultId, Name = "Assigned Vault" };
            var unassigned = new ConnectionGroup { Id = unassignedVaultId, Name = "Unassigned Vault" };
            db.ConnectionGroups.AddRange(assigned, unassigned);

            var user = new User { Id = userId, Email = "user@x", Name = "user", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(assigned);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.GetAsync("/api/vaults");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var vaults = await res.Content.ReadFromJsonAsync<List<ConnectionGroupDto>>();
        Assert.NotNull(vaults);
        Assert.Single(vaults);
        Assert.Equal(assignedVaultId, vaults[0].Id);
    }

    [Fact]
    public async Task GetVaults_ReturnsAssignedVaultsViaGroups_ForUser()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var assignedVaultId = Guid.NewGuid();
        var unassignedVaultId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var assigned = new ConnectionGroup { Id = assignedVaultId, Name = "Group Vault" };
            var unassigned = new ConnectionGroup { Id = unassignedVaultId, Name = "Unassigned Vault" };
            db.ConnectionGroups.AddRange(assigned, unassigned);

            var userGroup = new UserGroup { Id = groupId, Name = "Test Group" };
            userGroup.Vaults.Add(assigned);
            db.UserGroups.Add(userGroup);

            var user = new User { Id = userId, Email = "user@x", Name = "user", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.Groups.Add(userGroup);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.GetAsync("/api/vaults");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var vaults = await res.Content.ReadFromJsonAsync<List<ConnectionGroupDto>>();
        Assert.NotNull(vaults);
        Assert.Single(vaults);
        Assert.Equal(assignedVaultId, vaults[0].Id);
    }

    [Fact]
    public async Task GetVaults_ReturnsUnauthorized_WhenNoProfile()
    {
        var vault1Id = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vault1Id, Name = "Vault A" });
        });

        // Use a client without headers so it is unauthenticated. But TestAuthHandler returns NoResult.
        // If we set a random userId it will be authenticated but with no user in the DB.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var res = await client.GetAsync("/api/vaults");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
