using System.Collections.Concurrent;
using System.Net;
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

public class ConnectionsControllerTests
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
    public async Task ProbeConnectionsBulk_ReturnsReachability()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connId1 = Guid.NewGuid();
        var connId2 = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);
            var host = new Backend.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" };
            db.Hosts.Add(host);

            db.Connections.Add(new Connection { Id = connId1, Name = "c1", Protocol = "ssh", HostId = hostId, ConnectionGroupId = vaultId, Settings = "{}" });
            db.Connections.Add(new Connection { Id = connId2, Name = "c2", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultId, Settings = "{}" });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);

        var ids = new List<Guid> { connId1, connId2 };
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", ids);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        // Accessible connections with a host return "up" or "down" — never null or error statuses.
        Assert.True(results[connId1] == "up" || results[connId1] == "down",
            $"Expected 'up' or 'down' for connId1, got '{results[connId1]}'");
        Assert.True(results[connId2] == "up" || results[connId2] == "down",
            $"Expected 'up' or 'down' for connId2, got '{results[connId2]}'");
    }

    [Fact]
    public async Task ProbeConnectionsBulk_MissingId_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", new List<Guid> { missingId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal("not_found", results[missingId]);
    }

    [Fact]
    public async Task ProbeConnectionsBulk_ForbiddenId_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var ownVaultId = Guid.NewGuid();
        var otherVaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var forbiddenConnId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var ownVault = new ConnectionGroup { Id = ownVaultId, Name = "mine" };
            var otherVault = new ConnectionGroup { Id = otherVaultId, Name = "theirs" };
            db.ConnectionGroups.AddRange(ownVault, otherVault);

            db.Hosts.Add(new Backend.Models.Host { Id = hostId, Name = "h", Address = "10.0.0.1" });
            // This connection belongs to a vault the user has no access to.
            db.Connections.Add(new Connection { Id = forbiddenConnId, Name = "secret", Protocol = "rdp", HostId = hostId, ConnectionGroupId = otherVaultId, Settings = "{}" });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(ownVault); // user only has access to ownVault
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", new List<Guid> { forbiddenConnId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal("forbidden", results[forbiddenConnId]);
    }

    [Fact]
    public async Task ProbeConnectionsBulk_HostlessConnection_ReturnsNoHost()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostlessConnId = Guid.NewGuid();
        // A host ID that is never inserted into the database — Host nav property will be null.
        var nonExistentHostId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);

            // Connection references a host that doesn't exist — Host navigation property will be null.
            db.Connections.Add(new Connection { Id = hostlessConnId, Name = "no-host", Protocol = "rdp", HostId = nonExistentHostId, ConnectionGroupId = vaultId, Settings = "{}" });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", new List<Guid> { hostlessConnId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal("no_host", results[hostlessConnId]);
    }
}
