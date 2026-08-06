using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
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
        Assert.Equal(2, adminList.Total);

        var userList = await userClient.GetFromJsonAsync<RecordingsListDto>("/api/recordings", TestJson.Options);
        Assert.NotNull(userList);
        Assert.Equal(1, userList.Total);
        Assert.Equal(sharedConnectionId, userList.Items[0].ConnectionId);
    }

    [Fact]
    public async Task Stream_AdminGetsBytes_WhenAvailable()
    {
        var adminId = Guid.NewGuid();
        var recordingId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var fakeStorage = new FakeRecordingStorageService("hello recording");

        await using var factory = new TestWebApplicationFactory(
            db =>
            {
                var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, admin, AppRoles.Admin);
                db.Users.Add(admin);

                db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
                db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId });
                db.Recordings.Add(new Recording
                {
                    Id = recordingId,
                    SessionId = "abc123",
                    ConnectionId = connectionId,
                    UserId = adminId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-2),
                    EndedAt = DateTime.UtcNow,
                    Status = RecordingStatus.Available,
                    StorageKey = "k.guac",
                    StorageType = RecordingStorageType.Local,
                });
            },
            overrideServices: services =>
            {
                services.RemoveAll<IRecordingStorageFactory>();
                services.AddSingleton<IRecordingStorageFactory>(new FakeRecordingStorageFactory(fakeStorage));
            });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.GetAsync($"/api/recordings/{recordingId}/stream");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("hello recording", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Stream_ReturnsNotFound_WhenStatusIsNotAvailable()
    {
        var adminId = Guid.NewGuid();
        var recordingId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId });
            db.Recordings.Add(new Recording
            {
                Id = recordingId,
                SessionId = "abc123",
                ConnectionId = connectionId,
                UserId = adminId,
                StartedAt = DateTime.UtcNow.AddMinutes(-2),
                Status = RecordingStatus.Uploading,
                StorageKey = "k.guac",
                StorageType = RecordingStorageType.Local,
            });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.GetAsync($"/api/recordings/{recordingId}/stream");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Stream_ReturnsNotFound_WhenCallerLacksConnectionAccess()
    {
        var userId = Guid.NewGuid();
        var recordingId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var hiddenConnectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
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
                Id = recordingId,
                SessionId = "abc",
                ConnectionId = hiddenConnectionId,
                UserId = userId,
                StartedAt = DateTime.UtcNow.AddMinutes(-2),
                EndedAt = DateTime.UtcNow,
                Status = RecordingStatus.Available,
                StorageKey = "h.guac",
                StorageType = RecordingStorageType.Local,
            });
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.GetAsync($"/api/recordings/{recordingId}/stream");

        // 404 (not 403) — existence is not leaked to non-authorized callers.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByConnectionAndStatus()
    {
        var adminId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = aId, Name = "a", Protocol = "rdp", HostId = hostId });
            db.Connections.Add(new Connection { Id = bId, Name = "b", Protocol = "rdp", HostId = hostId });

            db.Recordings.AddRange(
                NewRecording(adminId, aId, RecordingStatus.Available, "a-ok"),
                NewRecording(adminId, aId, RecordingStatus.Failed, "a-bad"),
                NewRecording(adminId, bId, RecordingStatus.Available, "b-ok"));
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // filter by connectionId
        var byConn = await client.GetFromJsonAsync<RecordingsListDto>(
            $"/api/recordings?connectionId={aId}", TestJson.Options);
        Assert.NotNull(byConn);
        Assert.Equal(2, byConn.Total);

        // filter by status
        var failed = await client.GetFromJsonAsync<RecordingsListDto>(
            "/api/recordings?status=Failed", TestJson.Options);
        Assert.NotNull(failed);
        Assert.Equal(1, failed.Total);
        Assert.Equal(RecordingStatus.Failed, failed.Items[0].Status);
    }

    [Fact]
    public async Task Delete_AdminPurgesRowAndCallsStorage()
    {
        var adminId = Guid.NewGuid();
        var recordingId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var fakeStorage = new FakeRecordingStorageService(payload: "x");

        await using var factory = new TestWebApplicationFactory(
            db =>
            {
                var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, admin, AppRoles.Admin);
                db.Users.Add(admin);

                db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
                db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId });
                db.Recordings.Add(new Recording
                {
                    Id = recordingId,
                    SessionId = "abc",
                    ConnectionId = connectionId,
                    UserId = adminId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-1),
                    EndedAt = DateTime.UtcNow,
                    Status = RecordingStatus.Available,
                    StorageKey = "purge.guac",
                    StorageType = RecordingStorageType.Local,
                });
            },
            overrideServices: services =>
            {
                services.RemoveAll<IRecordingStorageFactory>();
                services.AddSingleton<IRecordingStorageFactory>(new FakeRecordingStorageFactory(fakeStorage));
            });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.DeleteAsync($"/api/recordings/{recordingId}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Contains("purge.guac", fakeStorage.DeletedKeys);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(db.Recordings.FirstOrDefault(r => r.Id == recordingId));
    }

    private static Recording NewRecording(Guid userId, Guid connectionId, RecordingStatus status, string seed) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = seed,
        ConnectionId = connectionId,
        UserId = userId,
        StartedAt = DateTime.UtcNow.AddMinutes(-5),
        EndedAt = DateTime.UtcNow,
        Status = status,
        StorageKey = $"{seed}.guac",
        StorageType = RecordingStorageType.Local,
    };

    [Fact]
    public async Task List_FiltersByUserId()
    {
        var adminId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
            db.Users.Add(new User { Id = otherId, Email = "o@x", Name = "o", IsActive = true, CreatedAt = DateTime.UtcNow });

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = connId, Name = "c", Protocol = "rdp", HostId = hostId });

            db.Recordings.AddRange(
                NewRecording(adminId, connId, RecordingStatus.Available, "admin-rec"),
                NewRecording(otherId, connId, RecordingStatus.Available, "other-rec"));
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var byUser = await client.GetFromJsonAsync<RecordingsListDto>(
            $"/api/recordings?userId={otherId}", TestJson.Options);

        Assert.NotNull(byUser);
        Assert.Equal(1, byUser.Total);
        Assert.Equal(otherId, byUser.Items[0].UserId);
    }

    [Fact]
    public async Task List_FiltersByVaultId()
    {
        var adminId = Guid.NewGuid();
        var vaultA = Guid.NewGuid();
        var vaultB = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connA = Guid.NewGuid();
        var connB = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultA, Name = "vault-a" });
            db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultB, Name = "vault-b" });

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = connA, Name = "ca", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultA });
            db.Connections.Add(new Connection { Id = connB, Name = "cb", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultB });

            db.Recordings.AddRange(
                NewRecording(adminId, connA, RecordingStatus.Available, "a"),
                NewRecording(adminId, connB, RecordingStatus.Available, "b"));
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var byVault = await client.GetFromJsonAsync<RecordingsListDto>(
            $"/api/recordings?vaultId={vaultA}", TestJson.Options);

        Assert.NotNull(byVault);
        Assert.Equal(1, byVault.Total);
        Assert.Equal(vaultA, byVault.Items[0].VaultId);
    }

    [Fact]
    public async Task List_FiltersByDateRange()
    {
        var adminId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = connId, Name = "c", Protocol = "rdp", HostId = hostId });

            db.Recordings.Add(new Recording
            {
                Id = Guid.NewGuid(),
                SessionId = "old",
                ConnectionId = connId,
                UserId = adminId,
                StartedAt = now.AddDays(-10),
                EndedAt = now.AddDays(-10).AddMinutes(5),
                Status = RecordingStatus.Available,
                StorageKey = "old.guac",
                StorageType = RecordingStorageType.Local,
            });
            db.Recordings.Add(new Recording
            {
                Id = Guid.NewGuid(),
                SessionId = "recent",
                ConnectionId = connId,
                UserId = adminId,
                StartedAt = now.AddHours(-1),
                EndedAt = now,
                Status = RecordingStatus.Available,
                StorageKey = "recent.guac",
                StorageType = RecordingStorageType.Local,
            });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var from = now.AddDays(-1).ToString("o");
        var to = now.AddDays(1).ToString("o");

        var inWindow = await client.GetFromJsonAsync<RecordingsListDto>(
            $"/api/recordings?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}",
            TestJson.Options);

        Assert.NotNull(inWindow);
        Assert.Equal(1, inWindow.Total);
        Assert.Equal("recent", inWindow.Items[0].SessionId);
    }

    [Fact]
    public async Task List_RespectsPagination()
    {
        var adminId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection { Id = connId, Name = "c", Protocol = "rdp", HostId = hostId });

            for (int i = 0; i < 5; i++)
            {
                db.Recordings.Add(NewRecording(adminId, connId, RecordingStatus.Available, $"rec-{i}"));
            }
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        var page1 = await client.GetFromJsonAsync<RecordingsListDto>(
            "/api/recordings?page=1&pageSize=2", TestJson.Options);
        Assert.NotNull(page1);
        Assert.Equal(5, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);

        var page2 = await client.GetFromJsonAsync<RecordingsListDto>(
            "/api/recordings?page=2&pageSize=2", TestJson.Options);
        Assert.NotNull(page2);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(2, page2.Page);

        var page3 = await client.GetFromJsonAsync<RecordingsListDto>(
            "/api/recordings?page=3&pageSize=2", TestJson.Options);
        Assert.NotNull(page3);
        Assert.Single(page3.Items);
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

internal sealed class FakeRecordingStorageFactory : IRecordingStorageFactory
{
    private readonly IRecordingStorageService _service;
    public FakeRecordingStorageFactory(IRecordingStorageService service) => _service = service;
    public Task<IRecordingStorageService> CreateAsync(CancellationToken cancellationToken = default) => Task.FromResult(_service);
    public IRecordingStorageService CreateFor(RecordingStorageType storageType) => _service;
}

internal sealed class FakeRecordingStorageService : IRecordingStorageService
{
    private readonly byte[] _payload;
    public List<string> DeletedKeys { get; } = [];
    public List<string> UploadedKeys { get; } = [];

    public FakeRecordingStorageService(string payload) => _payload = Encoding.UTF8.GetBytes(payload);

    public RecordingStorageType StorageType => RecordingStorageType.Local;

    public Task<RecordingUploadResult> UploadAsync(string localFilePath, string storageKey, CancellationToken cancellationToken)
    {
        UploadedKeys.Add(storageKey);
        return Task.FromResult(new RecordingUploadResult(storageKey, _payload.Length));
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        => Task.FromResult<Stream>(new MemoryStream(_payload));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        DeletedKeys.Add(storageKey);
        return Task.CompletedTask;
    }

    public Task<string?> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
