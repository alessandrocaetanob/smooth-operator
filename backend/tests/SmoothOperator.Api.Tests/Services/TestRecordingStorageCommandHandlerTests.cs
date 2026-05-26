using System.Threading;
using System.Threading.Tasks;
using Moq;
using SmoothOperator.Application.Features.RecordingStorageSettings.Commands;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public sealed class TestRecordingStorageCommandHandlerTests
{
    [Fact]
    public async Task Handle_OnSuccess_ReturnsSuccessAndWritesAudit()
    {
        var storage = new Mock<IRecordingStorageService>();
        storage.SetupGet(s => s.StorageType).Returns(RecordingStorageType.S3);
        storage.Setup(s => s.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var factory = new Mock<IRecordingStorageFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storage.Object);

        var audit = new Mock<IAuditService>();

        var sut = new TestRecordingStorageCommandHandler(factory.Object, audit.Object);
        var result = await sut.Handle(new TestRecordingStorageCommand(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        audit.Verify(a => a.WriteAsync(
            "recording.storage_test",
            "RecordingStorageSettings",
            null,
            It.IsAny<object>(),
            "success"), Times.Once);
    }

    [Fact]
    public async Task Handle_OnFailure_ReturnsErrorAndAuditsFailure()
    {
        var storage = new Mock<IRecordingStorageService>();
        storage.SetupGet(s => s.StorageType).Returns(RecordingStorageType.AzureBlob);
        storage.Setup(s => s.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Azure Blob: AuthFailed");

        var factory = new Mock<IRecordingStorageFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storage.Object);

        var audit = new Mock<IAuditService>();

        var sut = new TestRecordingStorageCommandHandler(factory.Object, audit.Object);
        var result = await sut.Handle(new TestRecordingStorageCommand(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Azure Blob: AuthFailed", result.Error);

        audit.Verify(a => a.WriteAsync(
            "recording.storage_test",
            "RecordingStorageSettings",
            null,
            It.IsAny<object>(),
            "failure"), Times.Once);
    }
}
