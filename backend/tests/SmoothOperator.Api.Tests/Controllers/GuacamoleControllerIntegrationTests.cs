using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Domain.Models;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class GuacamoleControllerIntegrationTests
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
    public async Task IssueTicket_WhenAuthorized_ReturnsTicket()
    {
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "test@test.com", Name = "Test User", IsActive = true };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            var host = new Host { Id = Guid.NewGuid(), Name = "Test Host", Address = "1.2.3.4" };
            db.Hosts.Add(host);

            var conn = new Connection 
            { 
                Id = connectionId, 
                Name = "Test Conn", 
                HostId = host.Id,
                Protocol = "rdp"
            };
            conn.Users.Add(user);
            db.Connections.Add(conn);
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.User);

        var res = await client.PostAsync($"/api/guacamole/ticket/{connectionId}", null);
        
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var content = await res.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content.Ticket));
    }

    [Fact]
    public async Task IssueTicket_WhenNoAccess_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "test@test.com", Name = "Test User", IsActive = true };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            var otherUser = new User { Id = otherUserId, Email = "other@test.com", Name = "Other User", IsActive = true };
            AttachRoles(db, otherUser, AppRoles.User);
            db.Users.Add(otherUser);

            var host = new Host { Id = Guid.NewGuid(), Name = "Test Host", Address = "1.2.3.4" };
            db.Hosts.Add(host);

            var conn = new Connection 
            { 
                Id = connectionId, 
                Name = "Test Conn", 
                HostId = host.Id 
            };
            conn.Users.Add(otherUser); // Only other user has access
            db.Connections.Add(conn);
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.User);

        var res = await client.PostAsync($"/api/guacamole/ticket/{connectionId}", null);
        
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IssueTicket_WhenConnectionNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "test@test.com", Name = "Test User", IsActive = true };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.User);

        var res = await client.PostAsync($"/api/guacamole/ticket/{Guid.NewGuid()}", null);
        
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Connect_NotWebSocket_ReturnsBadRequest()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/api/guacamole/connect/{Guid.NewGuid()}?ticket=some-ticket");
        
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }


    private class TicketResponse
    {
        public string Ticket { get; set; } = string.Empty;
    }
}
