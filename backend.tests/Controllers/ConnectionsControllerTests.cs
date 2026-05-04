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
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.True(results.ContainsKey(connId1));
        Assert.True(results.ContainsKey(connId2));
        // We don't assert the actual true/false reachability value as it depends on whether the ports are open on localhost, which they likely aren't.
        // The goal is just to ensure the endpoint functions and returns a value for each ID safely.
    }
}
