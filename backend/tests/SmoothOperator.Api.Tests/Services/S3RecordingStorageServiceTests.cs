using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Infrastructure.Services.Recording;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public sealed class S3RecordingStorageServiceTests
{
    [Fact]
    public void StorageType_IsS3()
    {
        var client = new Mock<IAmazonS3>().Object;
        using var sut = new S3RecordingStorageService(client, "bucket", null);
        Assert.Equal(RecordingStorageType.S3, sut.StorageType);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsResponseStream_FromAmazonS3()
    {
        var bytes = Encoding.UTF8.GetBytes("hello-s3");
        var responseStream = new MemoryStream(bytes);
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.GetObjectAsync(
                It.Is<GetObjectRequest>(r => r.BucketName == "bucket" && r.Key == "k.guac"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = responseStream });

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", null);
        await using var stream = await sut.OpenReadAsync("k.guac", CancellationToken.None);

        using var reader = new StreamReader(stream);
        Assert.Equal("hello-s3", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenReadAsync_AppliesPrefix()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream() });

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", "prod/");
        await sut.OpenReadAsync("k.guac", CancellationToken.None);

        s3.Verify(c => c.GetObjectAsync(
            It.Is<GetObjectRequest>(r => r.Key == "prod/k.guac"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_CallsDeleteObject_WithBucketAndKey()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", null);
        await sut.DeleteAsync("session.guac", CancellationToken.None);

        s3.Verify(c => c.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => r.BucketName == "bucket" && r.Key == "session.guac"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/../escape")]
    public async Task DeleteAsync_RejectsPathTraversal(string maliciousKey)
    {
        var s3 = new Mock<IAmazonS3>().Object;
        using var sut = new S3RecordingStorageService(s3, "bucket", null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.DeleteAsync(maliciousKey, CancellationToken.None));
    }

    [Fact]
    public async Task TestConnectionAsync_OnSuccess_ReturnsNull()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.GetBucketLocationAsync(It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetBucketLocationResponse());

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", null);
        var result = await sut.TestConnectionAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TestConnectionAsync_OnAmazonS3Exception_ReturnsFormattedError()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.GetBucketLocationAsync(It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access denied") { ErrorCode = "AccessDenied" });

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", null);
        var result = await sut.TestConnectionAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("AccessDenied", result);
        Assert.Contains("Access denied", result);
    }

    [Fact]
    public async Task TestConnectionAsync_OnGenericException_ReturnsFormattedError()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.GetBucketLocationAsync(It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", null);
        var result = await sut.TestConnectionAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("boom", result);
    }

    [Fact]
    public async Task NormalisePrefix_TrimsSlashesAndWhitespace()
    {
        // Test prefix normalisation indirectly: with a prefix like "  /prod/  ", the resolved
        // key should be "prod/k" (slashes and whitespace trimmed).
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        using var sut = new S3RecordingStorageService(s3.Object, "bucket", "  /prod/  ");
        await sut.DeleteAsync("k", CancellationToken.None);

        s3.Verify(c => c.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => r.Key == "prod/k"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesClient_AndIsIdempotent()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(c => c.Dispose()).Verifiable();

        var sut = new S3RecordingStorageService(s3.Object, "bucket", null);
        sut.Dispose();
        // Second dispose is a no-op (guarded by _disposed) — must not throw.
        var ex = Record.Exception(() => sut.Dispose());

        Assert.Null(ex);
        s3.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public void Constructor_WithS3StorageConfig_BuildsRealClient()
    {
        // The public ctor builds an AmazonS3Client internally. Just smoke-test that
        // it constructs without throwing on a valid config and reports the right type.
        var config = new S3StorageConfig(
            Bucket: "bucket",
            Region: "us-east-1",
            Endpoint: null,
            AccessKeyId: "AKIAEXAMPLE",
            SecretAccessKey: "secret",
            PathPrefix: null,
            ForcePathStyle: false);

        using var sut = new S3RecordingStorageService(config);
        Assert.Equal(RecordingStorageType.S3, sut.StorageType);
    }

    [Fact]
    public void Constructor_WithCustomEndpoint_OverridesRegion()
    {
        // Endpoint takes precedence over region. Smoke-test.
        var config = new S3StorageConfig(
            Bucket: "bucket",
            Region: "us-east-1",
            Endpoint: "https://minio.example:9000",
            AccessKeyId: "minio",
            SecretAccessKey: "miniosecret",
            PathPrefix: "prefix",
            ForcePathStyle: true);

        using var sut = new S3RecordingStorageService(config);
        Assert.Equal(RecordingStorageType.S3, sut.StorageType);
    }
}
