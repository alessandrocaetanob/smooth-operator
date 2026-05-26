using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Infrastructure.Services.Recording;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public sealed class AzureBlobRecordingStorageServiceTests
{
    private static (Mock<BlobContainerClient> container, Mock<BlobClient> blob) BuildMocks()
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();
        container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);
        return (container, blob);
    }

    [Fact]
    public void StorageType_IsAzureBlob()
    {
        var (container, _) = BuildMocks();
        var sut = new AzureBlobRecordingStorageService(container.Object, null);
        Assert.Equal(RecordingStorageType.AzureBlob, sut.StorageType);
    }

    [Fact]
    public async Task UploadAsync_StreamsFileToBlob_AndReturnsFileSize()
    {
        var (container, blob) = BuildMocks();
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, null, null, default), Mock.Of<Response>()));

        var tempPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempPath, "azure-payload");

        try
        {
            var sut = new AzureBlobRecordingStorageService(container.Object, null);
            var result = await sut.UploadAsync(tempPath, "session.guac", CancellationToken.None);

            Assert.Equal("session.guac", result.StorageKey);
            Assert.Equal(new FileInfo(tempPath).Length, result.SizeBytes);
            container.Verify(c => c.GetBlobClient("session.guac"), Times.Once);
            blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task UploadAsync_AppliesPrefix()
    {
        var (container, blob) = BuildMocks();
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, null, null, default), Mock.Of<Response>()));

        var tempPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempPath, "x");

        try
        {
            var sut = new AzureBlobRecordingStorageService(container.Object, "prod");
            await sut.UploadAsync(tempPath, "session.guac", CancellationToken.None);

            container.Verify(c => c.GetBlobClient("prod/session.guac"), Times.Once);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsTheBlobStream()
    {
        var (container, blob) = BuildMocks();
        var bytes = Encoding.UTF8.GetBytes("blob-body");
        var content = new MemoryStream(bytes);
        var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content);
        blob.Setup(b => b.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(downloadResult, Mock.Of<Response>()));

        var sut = new AzureBlobRecordingStorageService(container.Object, null);
        await using var stream = await sut.OpenReadAsync("session.guac", CancellationToken.None);

        using var reader = new StreamReader(stream);
        Assert.Equal("blob-body", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DeleteAsync_CallsDeleteIfExists()
    {
        var (container, blob) = BuildMocks();
        var response = Mock.Of<Response<bool>>();
        blob.Setup(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = new AzureBlobRecordingStorageService(container.Object, null);
        await sut.DeleteAsync("session.guac", CancellationToken.None);

        container.Verify(c => c.GetBlobClient("session.guac"), Times.Once);
        blob.Verify(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/../escape")]
    public async Task DeleteAsync_RejectsPathTraversal(string maliciousKey)
    {
        var (container, _) = BuildMocks();
        var sut = new AzureBlobRecordingStorageService(container.Object, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.DeleteAsync(maliciousKey, CancellationToken.None));
    }

    [Fact]
    public async Task TestConnectionAsync_OnSuccess_ReturnsNull()
    {
        var (container, _) = BuildMocks();
        container.Setup(c => c.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(),
                It.IsAny<System.Collections.Generic.IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(default, default), Mock.Of<Response>()));

        var sut = new AzureBlobRecordingStorageService(container.Object, null);
        var result = await sut.TestConnectionAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TestConnectionAsync_OnRequestFailedException_ReturnsFormattedError()
    {
        var (container, _) = BuildMocks();
        container.Setup(c => c.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(),
                It.IsAny<System.Collections.Generic.IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden", "AuthFailed", null));

        var sut = new AzureBlobRecordingStorageService(container.Object, null);
        var result = await sut.TestConnectionAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("AuthFailed", result);
    }

    [Fact]
    public async Task TestConnectionAsync_OnGenericException_ReturnsFormattedError()
    {
        var (container, _) = BuildMocks();
        container.Setup(c => c.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(),
                It.IsAny<System.Collections.Generic.IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom-azure"));

        var sut = new AzureBlobRecordingStorageService(container.Object, null);
        var result = await sut.TestConnectionAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("boom-azure", result);
    }

    [Fact]
    public void Constructor_DefaultsServiceUri_FromAccountName()
    {
        // Smoke-test the public ctor: with no BlobEndpoint, it uses the *.blob.core.windows.net default.
        var config = new AzureBlobStorageConfig(
            AccountName: "myaccount",
            AccountKey: System.Convert.ToBase64String(Encoding.UTF8.GetBytes("test-key-32-bytes-padding-1234")),
            ContainerName: "recordings",
            BlobEndpoint: null,
            PathPrefix: null);

        var sut = new AzureBlobRecordingStorageService(config);
        Assert.Equal(RecordingStorageType.AzureBlob, sut.StorageType);
    }

    [Fact]
    public void Constructor_WithCustomEndpoint_UsesProvidedUri()
    {
        // Smoke-test the public ctor: a custom BlobEndpoint (e.g., Azurite emulator).
        var config = new AzureBlobStorageConfig(
            AccountName: "devstoreaccount1",
            AccountKey: System.Convert.ToBase64String(Encoding.UTF8.GetBytes("test-key-32-bytes-padding-1234")),
            ContainerName: "recordings",
            BlobEndpoint: "http://127.0.0.1:10000/devstoreaccount1",
            PathPrefix: "  /prefix/  ");

        var sut = new AzureBlobRecordingStorageService(config);
        Assert.Equal(RecordingStorageType.AzureBlob, sut.StorageType);
    }
}
