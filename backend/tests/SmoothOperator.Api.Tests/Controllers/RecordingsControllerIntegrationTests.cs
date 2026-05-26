using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class RecordingsControllerIntegrationTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
            user.Roles.Add(db.Roles.First(r => r.Name == name));
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    [Fact]
    public async Task List_AdminSeesAll_NonAdminScopedToOwnAccessibleConnections()
    {
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var sharedConnectionId = Guid.NewGuid();
        var hiddenConnectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            var vault = new ConnectionGroup { Id = vaultId, Name = "test-vault" };
            vault.Users.Add(user); // user has access to vault
            db.ConnectionGroups.Add(vault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });

            // shared connection in user's vault
            db.Connections.Add(new Connection
            {
                Id = sharedConnectionId,
                Name = "shared",
                Protocol = "rdp",
                HostId = hostId,
                ConnectionGroupId = vaultId,
            });

            // hidden connection not in any vault user can see
            db.Connections.Add(new Connection
            {
                Id = hiddenConnectionId,
                Name = "hidden",
                Protocol = "rdp",
                HostId = hostId,
                ConnectionGroupId = null,
            });

            db.Recordings.Add(new Recording
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid().ToString("N"),
                ConnectionId = sharedConnectionId,
                UserId = userId,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                EndedAt = DateTime.UtcNow,
                Status = RecordingStatus.Available,
                StorageKey = "shared.guac",
                StorageType = RecordingStorageType.Local,
            });
            db.Recordings.Add(new Recording
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid().ToString("N"),
                ConnectionId = hiddenConnectionId,
                UserId = adminId,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                EndedAt = DateTime.UtcNow,
                Status = RecordingStatus.Available,
                StorageKey = "hidden.guac",
                StorageType = RecordingStorageType.Local,
            });
        });

        var adminClient = AsUser(factory, adminId, AppRoles.Admin);
        var userClient = AsUser(factory, userId, AppRoles.User);

        var adminList = await adminClient.GetFromJsonAsync<RecordingsListDto>("/api/recordings", TestJson.Options);
        Assert.NotNull(adminList);
        Assert.Equal(2, adminList!.Total);

        var userList = await userClient.GetFromJsonAsync<RecordingsListDto>("/api/recordings", TestJson.Options);
        Assert.NotNull(userList);
        Assert.Equal(1, userList!.Total);
        Assert.Equal(sharedConnectionId, userList.Items[0].ConnectionId);
    }

    [Fact]
    public async Task Delete_RequiresOwnerOrAdmin()
    {
        var userId = Guid.NewGuid();
        var recordingId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId });

            db.Recordings.Add(new Recording
            {
                Id = recordingId,
                SessionId = Guid.NewGuid().ToString("N"),
                ConnectionId = connectionId,
                UserId = userId,
                StartedAt = DateTime.UtcNow.AddMinutes(-1),
                EndedAt = DateTime.UtcNow,
                Status = RecordingStatus.Available,
                StorageKey = "k.guac",
                StorageType = RecordingStorageType.Local,
            });
        });

        var userClient = AsUser(factory, userId, AppRoles.User);
        var res = await userClient.DeleteAsync($"/api/recordings/{recordingId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
