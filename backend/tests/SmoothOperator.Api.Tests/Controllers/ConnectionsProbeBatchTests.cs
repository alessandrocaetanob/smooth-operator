using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

/// <summary>
/// Integration tests for POST /api/connections/probe-batch.
/// These tests focus on access-control behaviour and partial-result handling.
/// They do not exercise real TCP probes — connections point to an unrouteable
/// address (TEST-NET 192.0.2.x) so the TCP connect always times out/fails, but
/// that is acceptable here because we are testing authorization, not liveness.
/// </summary>
public class ConnectionsProbeBatchTests
{
    // ---- helpers -----------------------------------------------------------

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
            user.Roles.Add(db.Roles.First(r => r.Name == name));
    }

    // ---- tests -------------------------------------------------------------

    [Fact]
    public async Task ProbeBatch_RequiresAuthentication()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient(); // no identity headers
        var res = await client.PostAsJsonAsync("/api/connections/probe-batch", new List<Guid> { Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProbeBatch_ReturnsOnlyConnectionsAccessibleToUser()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var accessibleId = Guid.NewGuid();
        var inaccessibleId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "my-vault" };
            db.ConnectionGroups.Add(vault);

            var host = new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "192.0.2.1" };
            db.Hosts.Add(host);

            // Accessible: belongs to the vault the user is assigned to
            db.Connections.Add(new Connection
            {
                Id = accessibleId,
                Name = "accessible",
                Protocol = "rdp",
                HostId = hostId,
                ConnectionGroupId = vaultId,
                Settings = "{}"
            });

            // Inaccessible: no vault, no direct assignment
            db.Connections.Add(new Connection
            {
                Id = inaccessibleId,
                Name = "inaccessible",
                Protocol = "rdp",
                HostId = hostId,
                Settings = "{}"
            });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(vault);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync(
            "/api/connections/probe-batch",
            new List<Guid> { accessibleId, inaccessibleId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();
        Assert.NotNull(body);

        // Accessible connection must appear in the result
        Assert.True(body.ContainsKey(accessibleId), "Accessible connection should be in the result");

        // Inaccessible connection must NOT appear — returning it would leak its existence
        Assert.False(body.ContainsKey(inaccessibleId), "Inaccessible connection must not be leaked in the result");
    }

    [Fact]
    public async Task ProbeBatch_AdminSeesAllRequestedConnections()
    {
        var adminId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var conn1Id = Guid.NewGuid();
        var conn2Id = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var host = new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "192.0.2.1" };
            db.Hosts.Add(host);

            db.Connections.Add(new Connection { Id = conn1Id, Name = "c1", Protocol = "ssh", HostId = hostId, Settings = "{}" });
            db.Connections.Add(new Connection { Id = conn2Id, Name = "c2", Protocol = "rdp", HostId = hostId, Settings = "{}" });

            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.PostAsJsonAsync(
            "/api/connections/probe-batch",
            new List<Guid> { conn1Id, conn2Id });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey(conn1Id));
        Assert.True(body.ContainsKey(conn2Id));
    }

    [Fact]
    public async Task ProbeBatch_EmptyList_ReturnsEmptyDictionary()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-batch", new List<Guid>());

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task ProbeBatch_NonexistentId_IsAbsentFromResponse()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.Admin);
            db.Users.Add(u);
        });

        var phantomId = Guid.NewGuid();
        var client = AsUser(factory, userId, AppRoles.Admin);
        var res = await client.PostAsJsonAsync("/api/connections/probe-batch", new List<Guid> { phantomId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();
        Assert.NotNull(body);
        Assert.False(body.ContainsKey(phantomId), "Non-existent connection must not appear in the response");
    }
}
