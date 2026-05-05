using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class HostsControllerIntegrationTests
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
    public async Task Hosts_Lifecycle()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Create Host
        var createReq = new CreateHostDto { Name = "Web Server", Address = "192.168.1.10" };
        var createRes = await client.PostAsJsonAsync("/api/hosts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<HostDto>();
        Assert.NotNull(created);
        Assert.Equal("Web Server", created.Name);
        var hostId = created.Id;

        // 2. Get Host
        var getRes = await client.GetAsync($"/api/hosts/{hostId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<HostDto>();
        Assert.NotNull(fetched);
        Assert.Equal(hostId, fetched.Id);

        // 3. List
        var listRes = await client.GetAsync("/api/hosts");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<HostDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, h => h.Id == hostId);

        // 4. Update
        var updateReq = new CreateHostDto { Name = "DB Server", Address = "10.0.0.5" };
        var updateRes = await client.PutAsJsonAsync($"/api/hosts/{hostId}", updateReq);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        // 5. Delete
        var delRes = await client.DeleteAsync($"/api/hosts/{hostId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }
}
