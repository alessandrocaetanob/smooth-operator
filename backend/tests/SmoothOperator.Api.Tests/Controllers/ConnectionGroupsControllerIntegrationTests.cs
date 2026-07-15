using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Controllers;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class ConnectionGroupsControllerIntegrationTests
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
    public async Task Vault_Lifecycle_And_Assignments()
    {
        var adminId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var userGroupId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.Users.Add(new User { Id = user1Id, Email = "u1@x", Name = "u1", IsActive = true, CreatedAt = DateTime.UtcNow });
            db.UserGroups.Add(new UserGroup { Id = userGroupId, Name = "Test Group" });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Create Vault
        var createReq = new CreateConnectionGroupDto { Name = "Core Vault" };
        var createRes = await client.PostAsJsonAsync("/api/vaults", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdVault = await createRes.Content.ReadFromJsonAsync<ConnectionGroupDto>(TestJson.Options);
        Assert.NotNull(createdVault);
        Assert.Equal("Core Vault", createdVault.Name);
        var vaultId = createdVault.Id;

        // 2. Update Vault
        var updateReq = new CreateConnectionGroupDto { Name = "Main Vault" };
        var updateRes = await client.PutAsJsonAsync($"/api/vaults/{vaultId}", updateReq);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        // 3. Set Assignments
        var assignReq = new VaultAssignmentsDto { UserIds = [user1Id], GroupIds = [userGroupId] };
        var assignRes = await client.PutAsJsonAsync($"/api/vaults/{vaultId}/assignments", assignReq);
        Assert.Equal(HttpStatusCode.NoContent, assignRes.StatusCode);

        // 4. Get Assignments
        var getAssignRes = await client.GetAsync($"/api/vaults/{vaultId}/assignments");
        Assert.Equal(HttpStatusCode.OK, getAssignRes.StatusCode);
        var assignments = await getAssignRes.Content.ReadFromJsonAsync<VaultAssignmentsDto>();
        Assert.NotNull(assignments);
        Assert.Single(assignments.UserIds);
        Assert.Single(assignments.GroupIds);

        // 5. Get Effective Users
        var getEffRes = await client.GetAsync($"/api/vaults/{vaultId}/effective-users");
        Assert.Equal(HttpStatusCode.OK, getEffRes.StatusCode);
        var effUsers = await getEffRes.Content.ReadFromJsonAsync<VaultEffectiveUsersDto>();
        Assert.NotNull(effUsers);
        Assert.Contains(effUsers.Users, u => u.Id == user1Id);

        // 6. Delete Vault
        var delRes = await client.DeleteAsync($"/api/vaults/{vaultId}");
        Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode); // Fails because users/groups are assigned

        // Clear assignments
        await client.PutAsJsonAsync($"/api/vaults/{vaultId}/assignments", new VaultAssignmentsDto());

        var delRes2 = await client.DeleteAsync($"/api/vaults/{vaultId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes2.StatusCode);
    }

    [Fact]
    public async Task ListVaults_ReturnsExistingVaults_ForAdmin()
    {
        var adminId = Guid.NewGuid();
        var vaultA = Guid.NewGuid();
        var vaultB = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultA, Name = "A Team Vault" });
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultB, Name = "B Team Vault" });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var response = await client.GetAsync("/api/vaults");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var vaults = await response.Content.ReadFromJsonAsync<List<ConnectionGroupDto>>(TestJson.Options);
        Assert.NotNull(vaults);
        Assert.Contains(vaults, v => v.Id == vaultA);
        Assert.Contains(vaults, v => v.Id == vaultB);
    }

    [Fact]
    public async Task FileTransferTimeout_RoundTrips_Validates_AndFlowsToConnections()
    {
        var adminId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
            db.Hosts.Add(new Host { Id = hostId, Name = "h", Address = "10.0.0.1" });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // Create with a timeout + policy
        var createRes = await client.PostAsJsonAsync("/api/vaults", new CreateConnectionGroupDto
        {
            Name = "Timeout Vault",
            FileTransferPolicy = Domain.Enums.FileTransferPolicy.Both,
            FileTransferTimeoutSeconds = 45,
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var vault = await createRes.Content.ReadFromJsonAsync<ConnectionGroupDto>(TestJson.Options);
        Assert.NotNull(vault);
        Assert.Equal(45, vault.FileTransferTimeoutSeconds);

        // GET round-trip
        var listRes = await client.GetFromJsonAsync<List<ConnectionGroupDto>>("/api/vaults", TestJson.Options);
        Assert.Equal(45, listRes!.Single(v => v.Id == vault.Id).FileTransferTimeoutSeconds);

        // Out-of-range values are rejected by validation (Range 5-600)
        var badRes = await client.PostAsJsonAsync("/api/vaults", new
        {
            name = "Bad Vault",
            fileTransferTimeoutSeconds = 3,
        });
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        // The vault's timeout surfaces as the effective value on its connections
        var connRes = await client.PostAsJsonAsync("/api/connections", new CreateConnectionDto
        {
            Name = "ssh-conn",
            Protocol = "ssh",
            HostId = hostId,
            ConnectionGroupId = vault.Id,
        });
        Assert.Equal(HttpStatusCode.Created, connRes.StatusCode);

        var connections = await client.GetFromJsonAsync<List<ConnectionDto>>("/api/connections", TestJson.Options);
        var conn = connections!.Single(c => c.Name == "ssh-conn");
        Assert.Equal(45, conn.EffectiveFileTransferTimeoutSeconds);

        // Clearing back to null (app default)
        var clearRes = await client.PutAsJsonAsync($"/api/vaults/{vault.Id}", new CreateConnectionGroupDto
        {
            Name = "Timeout Vault",
            FileTransferPolicy = Domain.Enums.FileTransferPolicy.Both,
            FileTransferTimeoutSeconds = null,
        });
        Assert.Equal(HttpStatusCode.NoContent, clearRes.StatusCode);
        var after = await client.GetFromJsonAsync<List<ConnectionGroupDto>>("/api/vaults", TestJson.Options);
        Assert.Null(after!.Single(v => v.Id == vault.Id).FileTransferTimeoutSeconds);
    }
}
