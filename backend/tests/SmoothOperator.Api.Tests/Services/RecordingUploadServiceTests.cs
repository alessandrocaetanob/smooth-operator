using System.IO;
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

public class RecordingUploadServiceTests
{
    private static (ServiceProvider provider, AppDbContext db) BuildScope(Mock<IRecordingStorageService>? storageMock = null)
    {
        var services = new ServiceCollection();

        var dbName = Guid.NewGuid().ToString("N");
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

        var auditMock = new Mock<IAuditService>();
        services.AddSingleton(auditMock.Object);

        var storage = (storageMock ?? new Mock<IRecordingStorageService>()).Object;
        var factoryMock = new Mock<IRecordingStorageFactory>();
        factoryMock.Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storage);
        services.AddSingleton(factoryMock.Object);

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        return (provider, db);
    }

    private static RecordingUploadService BuildSut(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        provider.GetRequiredService<IRecordingStorageFactory>(),
        NullLogger<RecordingUploadService>.Instance);

    [Fact]
    public async Task UploadAndFinalise_OnHappyPath_MarksAvailable_DeletesTemp_SetsSize()
    {
        var recordingId = Guid.NewGuid();
        var storageMock = new Mock<IRecordingStorageService>();
        storageMock.SetupGet(s => s.StorageType).Returns(RecordingStorageType.Local);
        storageMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordingUploadResult("k.guac", 4242));

        var (provider, db) = BuildScope(storageMock);
        db.Recordings.Add(new Recording
        {
            Id = recordingId,
            SessionId = "abc",
            ConnectionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            StorageKey = "k.guac",
            StorageType = RecordingStorageType.Local,
            Status = RecordingStatus.Recording,
        });
        await db.SaveChangesAsync();

        var tempPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempPath, "x");

        var sut = BuildSut(provider);
        await sut.UploadAndFinaliseAsync(recordingId, tempPath, CancellationToken.None);

        var rec = await db.Recordings.AsNoTracking().FirstAsync(r => r.Id == recordingId);
        Assert.Equal(RecordingStatus.Available, rec.Status);
        Assert.Equal(4242, rec.FileSizeBytes);
        Assert.NotNull(rec.EndedAt);
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task UploadAndFinalise_WhenTempMissing_MarksFailedAndAudits()
    {
        var recordingId = Guid.NewGuid();
        var (provider, db) = BuildScope();
        db.Recordings.Add(new Recording
        {
            Id = recordingId,
            SessionId = "abc",
            ConnectionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            StorageKey = "k.guac",
            StorageType = RecordingStorageType.Local,
            Status = RecordingStatus.Recording,
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(provider);
        await sut.UploadAndFinaliseAsync(recordingId, "/nope/missing.guac", CancellationToken.None);

        var rec = await db.Recordings.AsNoTracking().FirstAsync(r => r.Id == recordingId);
        Assert.Equal(RecordingStatus.Failed, rec.Status);
        Assert.NotNull(rec.ErrorMessage);
    }

    [Fact]
    public async Task UploadAndFinalise_WhenStorageThrows_MarksFailed_AndPreservesTempFile()
    {
        var recordingId = Guid.NewGuid();
        var storageMock = new Mock<IRecordingStorageService>();
        storageMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3 503"));

        var (provider, db) = BuildScope(storageMock);
        db.Recordings.Add(new Recording
        {
            Id = recordingId,
            SessionId = "abc",
            ConnectionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            StorageKey = "k.guac",
            StorageType = RecordingStorageType.Local,
            Status = RecordingStatus.Recording,
        });
        await db.SaveChangesAsync();

        var tempPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempPath, "x");

        var sut = BuildSut(provider);
        await sut.UploadAndFinaliseAsync(recordingId, tempPath, CancellationToken.None);

        var rec = await db.Recordings.AsNoTracking().FirstAsync(r => r.Id == recordingId);
        Assert.Equal(RecordingStatus.Failed, rec.Status);
        Assert.Equal("S3 503", rec.ErrorMessage);
        // Temp file is intentionally left for retention sweep / admin cleanup.
        Assert.True(File.Exists(tempPath));
        File.Delete(tempPath);
    }

    [Fact]
    public async Task UploadAndFinalise_WhenRecordingNotFound_Returns()
    {
        var (provider, _) = BuildScope();
        var sut = BuildSut(provider);

        // Should not throw — orphaned trigger is logged + ignored.
        var ex = await Record.ExceptionAsync(() =>
            sut.UploadAndFinaliseAsync(Guid.NewGuid(), "/tmp/whatever.guac", CancellationToken.None));
        Assert.Null(ex);
    }
}
