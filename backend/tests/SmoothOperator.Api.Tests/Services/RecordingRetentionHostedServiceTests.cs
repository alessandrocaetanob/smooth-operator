using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services.Recording;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public sealed class RecordingRetentionHostedServiceTests
{
    private static (ServiceProvider provider, AppDbContext db, Mock<IRecordingStorageService> storage, Mock<IAuditService> audit) BuildScope()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString("N");
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

        var audit = new Mock<IAuditService>();
        services.AddSingleton(audit.Object);

        var storage = new Mock<IRecordingStorageService>();
        var factory = new Mock<IRecordingStorageFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(storage.Object);
        services.AddSingleton(factory.Object);

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        return (provider, db, storage, audit);
    }

    private static Task InvokeSweepAsync(ServiceProvider provider)
    {
        var sut = new RecordingRetentionHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RecordingRetentionHostedService>.Instance);
        var method = typeof(RecordingRetentionHostedService).GetMethod(
            "SweepOnceAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;
    }

    [Fact]
    public async Task SweepOnce_DeletesExpiredAndAudits()
    {
        var (provider, db, storage, audit) = BuildScope();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        // Default retention 1 day, set on the settings row.
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.Local,
            RetentionDays = 1,
        });
        db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultId, Name = "v", RecordingRetentionDays = null });
        db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
        db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultId });

        var expiredId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        db.Recordings.Add(new Recording
        {
            Id = expiredId,
            SessionId = "old",
            ConnectionId = connectionId,
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-5),
            EndedAt = DateTime.UtcNow.AddDays(-3),
            Status = RecordingStatus.Available,
            StorageKey = "old.guac",
            StorageType = RecordingStorageType.Local,
        });
        db.Recordings.Add(new Recording
        {
            Id = freshId,
            SessionId = "new",
            ConnectionId = connectionId,
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            EndedAt = DateTime.UtcNow.AddMinutes(-2),
            Status = RecordingStatus.Available,
            StorageKey = "new.guac",
            StorageType = RecordingStorageType.Local,
        });
        await db.SaveChangesAsync();

        await InvokeSweepAsync(provider);

        var expired = await db.Recordings.AsNoTracking().FirstAsync(r => r.Id == expiredId);
        var fresh = await db.Recordings.AsNoTracking().FirstAsync(r => r.Id == freshId);
        Assert.Equal(RecordingStatus.Deleted, expired.Status);
        Assert.Equal(RecordingStatus.Available, fresh.Status);

        storage.Verify(s => s.DeleteAsync("old.guac", It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.WriteAsync(
            "recording.retention_deleted",
            "Recording",
            expiredId.ToString(),
            It.IsAny<object>(),
            It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SweepOnce_PerVaultRetentionOverridesDefault()
    {
        var (provider, db, storage, _) = BuildScope();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        // Default 1 day but the vault keeps forever (0 = keep forever).
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.Local,
            RetentionDays = 1,
        });
        db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultId, Name = "v", RecordingRetentionDays = 0 });
        db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
        db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultId });

        var id = Guid.NewGuid();
        db.Recordings.Add(new Recording
        {
            Id = id,
            SessionId = "old",
            ConnectionId = connectionId,
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-30),
            EndedAt = DateTime.UtcNow.AddDays(-28),
            Status = RecordingStatus.Available,
            StorageKey = "kept.guac",
            StorageType = RecordingStorageType.Local,
        });
        await db.SaveChangesAsync();

        await InvokeSweepAsync(provider);

        var rec = await db.Recordings.AsNoTracking().FirstAsync(r => r.Id == id);
        Assert.Equal(RecordingStatus.Available, rec.Status);
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SweepOnce_WithNoSettingsAndZeroDefault_KeepsEverything()
    {
        var (provider, db, storage, _) = BuildScope();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        // No settings row → default retention is 0 → keep forever.
        db.ConnectionGroups.Add(new ConnectionGroup { Id = vaultId, Name = "v" });
        db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
        db.Connections.Add(new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultId });
        db.Recordings.Add(new Recording
        {
            Id = Guid.NewGuid(),
            SessionId = "x",
            ConnectionId = connectionId,
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-100),
            EndedAt = DateTime.UtcNow.AddDays(-99),
            Status = RecordingStatus.Available,
            StorageKey = "old.guac",
            StorageType = RecordingStorageType.Local,
        });
        await db.SaveChangesAsync();

        await InvokeSweepAsync(provider);
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
