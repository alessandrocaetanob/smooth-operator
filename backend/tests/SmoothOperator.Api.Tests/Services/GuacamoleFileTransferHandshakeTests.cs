using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

/// <summary>
/// Pure-input tests for <c>GuacamoleProxyService.PrepareFileTransferCoreAsync</c> — the
/// policy → guacd-params decision tree for in-session file transfer (SFTP for SSH,
/// drive redirect for RDP). Exercises the full policy matrix without a live WebSocket
/// or guacd, mirroring <see cref="GuacamoleRecordingHandshakeTests"/>.
/// </summary>
public sealed class GuacamoleFileTransferHandshakeTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FileTransferOptions _options;
    private readonly string _tempDir;

    public GuacamoleFileTransferHandshakeTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(dbOptions);

        _tempDir = Path.Combine(Path.GetTempPath(), "so-ft-handshake-" + Guid.NewGuid().ToString("N"));
        _options = new FileTransferOptions { DrivePath = _tempDir };
    }

    public void Dispose()
    {
        _db.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private static Connection BuildConnection(string protocol, FileTransferPolicy? vaultPolicy, FileTransferPolicy? connectionOverride)
    {
        var vault = vaultPolicy == null ? null : new ConnectionGroup { Id = Guid.NewGuid(), Name = "vault", FileTransferPolicy = vaultPolicy.Value };
        return new Connection
        {
            Id = Guid.NewGuid(),
            Name = "c",
            Protocol = protocol,
            HostId = Guid.NewGuid(),
            ConnectionGroupId = vault?.Id,
            ConnectionGroup = vault,
            FileTransferPolicyOverride = connectionOverride,
        };
    }

    [Fact]
    public async Task Disabled_ReturnsOriginalOverridesUnchangedAndWritesNoAudit()
    {
        var conn = BuildConnection("rdp", FileTransferPolicy.Disabled, null);
        var seed = new Dictionary<string, string> { ["password"] = "secret" };

        var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
            conn, Guid.NewGuid(), "sess-1", "1.2.3.4", seed, _db, _options, NullLogger.Instance);

        Assert.Equal(FileTransferPolicy.Disabled, result.Policy);
        Assert.Null(result.DriveDir);
        Assert.Same(seed, result.SettingsOverrides);
        Assert.Empty(await _db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.False(Directory.Exists(_tempDir));
    }

    [Theory]
    [InlineData(FileTransferPolicy.DownloadOnly, "true", "false")]
    [InlineData(FileTransferPolicy.UploadOnly, "false", "true")]
    [InlineData(FileTransferPolicy.Both, "false", "false")]
    public async Task Ssh_EnablesSftpWithPolicyDirectionFlags(FileTransferPolicy policy, string disableUpload, string disableDownload)
    {
        var conn = BuildConnection("ssh", policy, null);

        var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
            conn, Guid.NewGuid(), "sess-ssh", "1.2.3.4", null, _db, _options, NullLogger.Instance);

        Assert.Equal(policy, result.Policy);
        Assert.Null(result.DriveDir); // SFTP reuses the primary SSH session — no local directory needed.
        Assert.Equal("true", result.SettingsOverrides!["enable-sftp"]);
        Assert.Equal(disableUpload, result.SettingsOverrides["sftp-disable-upload"]);
        Assert.Equal(disableDownload, result.SettingsOverrides["sftp-disable-download"]);
    }

    [Theory]
    [InlineData(FileTransferPolicy.DownloadOnly, "true", "false")]
    [InlineData(FileTransferPolicy.UploadOnly, "false", "true")]
    [InlineData(FileTransferPolicy.Both, "false", "false")]
    public async Task Rdp_EnablesDriveRedirectWithPolicyDirectionFlags_AndCreatesPerSessionDir(
        FileTransferPolicy policy, string disableUpload, string disableDownload)
    {
        var conn = BuildConnection("rdp", policy, null);

        var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
            conn, Guid.NewGuid(), "sess-rdp-42", "1.2.3.4", null, _db, _options, NullLogger.Instance);

        Assert.Equal(policy, result.Policy);
        var expectedDir = Path.Combine(_tempDir, "sess-rdp-42");
        Assert.Equal(expectedDir, result.DriveDir);
        Assert.True(Directory.Exists(expectedDir));
        Assert.Equal("true", result.SettingsOverrides!["enable-drive"]);
        Assert.Equal(expectedDir, result.SettingsOverrides["drive-path"]);
        Assert.Equal("true", result.SettingsOverrides["create-drive-path"]);
        Assert.Equal(disableUpload, result.SettingsOverrides["disable-upload"]);
        Assert.Equal(disableDownload, result.SettingsOverrides["disable-download"]);
    }

    [Fact]
    public async Task Vnc_ResolvesPolicyButSendsNoParams()
    {
        var conn = BuildConnection("vnc", FileTransferPolicy.Both, null);
        var seed = new Dictionary<string, string> { ["password"] = "p" };

        var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
            conn, Guid.NewGuid(), "sess-vnc", "1.2.3.4", seed, _db, _options, NullLogger.Instance);

        Assert.Equal(FileTransferPolicy.Both, result.Policy);
        Assert.Null(result.DriveDir);
        Assert.Same(seed, result.SettingsOverrides);
    }

    [Fact]
    public async Task ConnectionOverride_WinsOverVaultDefault()
    {
        var conn = BuildConnection("ssh", FileTransferPolicy.Disabled, FileTransferPolicy.Both);

        var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
            conn, Guid.NewGuid(), "sess", "ip", null, _db, _options, NullLogger.Instance);

        Assert.Equal(FileTransferPolicy.Both, result.Policy);
        Assert.Equal("true", result.SettingsOverrides!["enable-sftp"]);
    }

    [Fact]
    public async Task Rdp_MergesIntoExistingOverridesWithoutMutatingTheCaller()
    {
        var conn = BuildConnection("rdp", FileTransferPolicy.Both, null);
        var caller = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["password"] = "p" };

        var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
            conn, Guid.NewGuid(), "sess", "ip", caller, _db, _options, NullLogger.Instance);

        Assert.NotSame(caller, result.SettingsOverrides);
        Assert.Equal("p", result.SettingsOverrides!["password"]);
        Assert.Contains("drive-path", result.SettingsOverrides);
        Assert.False(caller.ContainsKey("drive-path"), "caller dict must not be mutated");
    }

    [Fact]
    public async Task Rdp_WhenDriveDirUnwritable_WritesSkippedAuditAndReturnsOriginal()
    {
        // Point at a path INSIDE an existing file — Directory.CreateDirectory throws
        // IOException because the parent is not a directory. Portable across Linux + Windows.
        var blockingFile = Path.Combine(Path.GetTempPath(), "so-ft-block-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(blockingFile, "x");
        try
        {
            var unwritable = new FileTransferOptions { DrivePath = Path.Combine(blockingFile, "subdir") };
            var conn = BuildConnection("rdp", FileTransferPolicy.Both, null);
            var seed = new Dictionary<string, string> { ["k"] = "v" };

            var result = await GuacamoleProxyService.PrepareFileTransferCoreAsync(
                conn, Guid.NewGuid(), "sess", "ip", seed, _db, unwritable, NullLogger.Instance);

            Assert.Null(result.DriveDir);
            Assert.Same(seed, result.SettingsOverrides);

            var audit = Assert.Single(await _db.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Equal("file_transfer.skipped", audit.Action);
            Assert.Equal("failure", audit.Outcome);
            using var doc = JsonDocument.Parse(audit.Details);
            Assert.Equal("drive_dir_unwritable", doc.RootElement.GetProperty("reason").GetString());
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }
}
