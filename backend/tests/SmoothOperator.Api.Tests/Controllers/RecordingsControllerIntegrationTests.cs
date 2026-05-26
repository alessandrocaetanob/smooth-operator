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
        Assert.Equal(2, adminList!.Total);

        var userList = await userClient.GetFromJsonAsync<RecordingsListDto>("/api/recordings", TestJson.Options);
        Assert.NotNull(userList);
        Assert.Equal(1, userList!.Total);
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
        Assert.Equal(2, byConn!.Total);

        // filter by status
        var failed = await client.GetFromJsonAsync<RecordingsListDto>(
            "/api/recordings?status=Failed", TestJson.Options);
        Assert.NotNull(failed);
        Assert.Equal(1, failed!.Total);
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
    public List<string> DeletedKeys { get; } = new();
    public List<string> UploadedKeys { get; } = new();

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
