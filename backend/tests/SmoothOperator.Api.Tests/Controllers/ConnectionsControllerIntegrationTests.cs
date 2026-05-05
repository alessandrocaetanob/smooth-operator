using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class ConnectionsControllerIntegrationTests
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
    public async Task Connections_Lifecycle_For_Admin()
    {
        var adminId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultId, Name = "Test Vault" });
            db.Hosts.Add(new Host { Id = hostId, Name = "Test Host", Address = "127.0.0.1" });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Create Connection
        var createReq = new CreateConnectionDto
        {
            Name = "Web Server",
            Protocol = "ssh",
            ConnectionGroupId = vaultId,
            HostId = hostId,
            Settings = "{\"port\":\"22\"}",
            Tags = new List<string> { "prod", "web" }
        };

        var createRes = await client.PostAsJsonAsync("/api/connections", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ConnectionDto>();
        Assert.NotNull(created);
        Assert.Equal("Web Server", created.Name);
        var connId = created.Id;

        // 2. Get All
        var getAllRes = await client.GetAsync("/api/connections");
        Assert.Equal(HttpStatusCode.OK, getAllRes.StatusCode);
        var list = await getAllRes.Content.ReadFromJsonAsync<List<ConnectionDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, c => c.Id == connId);

        // 3. Get Single
        var getOneRes = await client.GetAsync($"/api/connections/{connId}");
        Assert.Equal(HttpStatusCode.OK, getOneRes.StatusCode);
        var one = await getOneRes.Content.ReadFromJsonAsync<ConnectionDto>();
        Assert.NotNull(one);
        Assert.Equal("Web Server", one.Name);

        // 4. Update Connection
        var updateReq = new CreateConnectionDto
        {
            Name = "Web Server Updated",
            Protocol = "ssh",
            ConnectionGroupId = vaultId,
            HostId = hostId,
            Settings = "{\"port\":\"2222\"}",
            Tags = new List<string> { "prod", "web2" }
        };
        var updateRes = await client.PutAsJsonAsync($"/api/connections/{connId}", updateReq);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        // Verify update
        var getUpdated = await client.GetFromJsonAsync<ConnectionDto>($"/api/connections/{connId}");
        Assert.NotNull(getUpdated);
        Assert.Equal("Web Server Updated", getUpdated.Name);
        Assert.Contains("web2", getUpdated.Tags);

        // 5. Delete Connection
        var delRes = await client.DeleteAsync($"/api/connections/{connId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // Verify deletion
        var getDeleted = await client.GetAsync($"/api/connections/{connId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeleted.StatusCode);
    }
}
