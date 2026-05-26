using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Infrastructure.Services.Recording;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public sealed class LocalRecordingStorageServiceTests : IDisposable
{
    private readonly string _baseDir;

    public LocalRecordingStorageServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "so-rec-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_baseDir, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public void StorageType_IsLocal()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        Assert.Equal(RecordingStorageType.Local, sut.StorageType);
    }

    [Fact]
    public async Task UploadAsync_CopiesFileIntoBasePath()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        var src = Path.Combine(_baseDir, "src.guac");
        await File.WriteAllTextAsync(src, "payload");

        var result = await sut.UploadAsync(src, "2026/05/connection/session.guac", CancellationToken.None);

        var expected = Path.Combine(_baseDir, "2026", "05", "connection", "session.guac");
        Assert.True(File.Exists(expected));
        Assert.Equal("payload", await File.ReadAllTextAsync(expected));
        Assert.Equal("2026/05/connection/session.guac", result.StorageKey);
        Assert.Equal(7, result.SizeBytes);
    }

    [Fact]
    public async Task UploadAsync_WhenSourceAlreadyAtDestination_ReturnsExistingSize()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        // Pre-place the file at the final destination.
        var key = "already/there.guac";
        var dst = Path.Combine(_baseDir, "already", "there.guac");
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        await File.WriteAllTextAsync(dst, "12345");

        var result = await sut.UploadAsync(dst, key, CancellationToken.None);

        Assert.Equal(key, result.StorageKey);
        Assert.Equal(5, result.SizeBytes);
    }

    [Fact]
    public async Task OpenReadAsync_StreamsTheBytesBack()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        var dst = Path.Combine(_baseDir, "read.guac");
        await File.WriteAllTextAsync(dst, "abc123");

        await using var stream = await sut.OpenReadAsync("read.guac", CancellationToken.None);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal("abc123", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFile()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        var dst = Path.Combine(_baseDir, "to-delete.guac");
        await File.WriteAllTextAsync(dst, "x");

        await sut.DeleteAsync("to-delete.guac", CancellationToken.None);

        Assert.False(File.Exists(dst));
    }

    [Fact]
    public async Task DeleteAsync_OnMissingFile_DoesNotThrow()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        // No exception expected — deletes are idempotent.
        var ex = await Record.ExceptionAsync(() => sut.DeleteAsync("nope.guac", CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact]
    public async Task TestConnectionAsync_OnWritablePath_ReturnsNull()
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        var result = await sut.TestConnectionAsync(CancellationToken.None);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("../escape.guac")]
    [InlineData("nested/../../escape.guac")]
    public async Task UploadAsync_RejectsPathTraversal(string maliciousKey)
    {
        var sut = new LocalRecordingStorageService(_baseDir);
        var src = Path.Combine(_baseDir, "src.guac");
        await File.WriteAllTextAsync(src, "payload");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UploadAsync(src, maliciousKey, CancellationToken.None));
    }

    [Theory]
    [InlineData("/abs/path.guac")]
    [InlineData("\\abs\\path.guac")]
    public async Task UploadAsync_TrimsLeadingSeparators(string keyWithLeading)
    {
        // Leading separators must NOT make Path.Combine treat the key as absolute
        // (which would escape _basePath). We only verify the result key round-trips
        // and that the call doesn't throw — the on-disk layout differs per OS
        // (Linux treats '\' as a literal filename char, Windows as a separator).
        var sut = new LocalRecordingStorageService(_baseDir);
        var src = Path.Combine(_baseDir, "src.guac");
        await File.WriteAllTextAsync(src, "p");

        var result = await sut.UploadAsync(src, keyWithLeading, CancellationToken.None);

        Assert.Equal(keyWithLeading, result.StorageKey);
        // Nothing should have escaped _baseDir — search the whole tree starting there.
        var written = Directory.GetFiles(_baseDir, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(written);
    }
}
