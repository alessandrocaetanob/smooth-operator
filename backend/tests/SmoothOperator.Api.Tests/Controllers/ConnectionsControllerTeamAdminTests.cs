using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class ConnectionsControllerTeamAdminTests
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
    public async Task TeamAdmin_CanCreateConnection_InAssignedVault()
    {
        var teamAdminId = Guid.NewGuid();
        var assignedVaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var assigned = new ConnectionGroup { Id = assignedVaultId, Name = "assigned" };
            db.ConnectionGroups.Add(assigned);
            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "10.0.0.1" });

            var ta = new User { Id = teamAdminId, Email = "ta@x", Name = "ta", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, ta, AppRoles.TeamAdmin);
            ta.ConnectionGroups.Add(assigned);
            db.Users.Add(ta);
        });

        var client = AsUser(factory, teamAdminId, AppRoles.TeamAdmin);
        var dto = new CreateConnectionDto
        {
            Name = "ssh-1",
            Protocol = "ssh",
            HostId = hostId,
            ConnectionGroupId = assignedVaultId,
            Settings = "{}"
        };
        var res = await client.PostAsJsonAsync("/api/connections", dto);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task TeamAdmin_CannotCreateConnection_InOtherVault()
    {
        var teamAdminId = Guid.NewGuid();
        var assignedVaultId = Guid.NewGuid();
        var foreignVaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var assigned = new ConnectionGroup { Id = assignedVaultId, Name = "mine" };
            var foreign = new ConnectionGroup { Id = foreignVaultId, Name = "yours" };
            db.ConnectionGroups.AddRange(assigned, foreign);
            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "10.0.0.1" });

            var ta = new User { Id = teamAdminId, Email = "ta@x", Name = "ta", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, ta, AppRoles.TeamAdmin);
            ta.ConnectionGroups.Add(assigned);
            db.Users.Add(ta);
        });

        var client = AsUser(factory, teamAdminId, AppRoles.TeamAdmin);
        var dto = new CreateConnectionDto
        {
            Name = "ssh-evil",
            Protocol = "ssh",
            HostId = hostId,
            ConnectionGroupId = foreignVaultId,
            Settings = "{}"
        };
        var res = await client.PostAsJsonAsync("/api/connections", dto);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task PlainUser_CannotCreateConnection_AnyVault()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);
            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "10.0.0.1" });
            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var dto = new CreateConnectionDto
        {
            Name = "n",
            Protocol = "ssh",
            HostId = hostId,
            ConnectionGroupId = vaultId,
            Settings = "{}"
        };
        var res = await client.PostAsJsonAsync("/api/connections", dto);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
