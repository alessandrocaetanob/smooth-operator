using System.IO;
using System.Text.Json;
using System.Threading;
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
/// Pure-input tests for the recording handshake logic extracted into
/// <c>GuacamoleProxyService.PrepareRecordingCoreAsync</c>. These exercise the full
/// truth table (recording disabled, enabled+writable, enabled+unwritable) and the
/// "recording.skipped" audit path without needing a live WebSocket or Redis.
/// </summary>
public sealed class GuacamoleRecordingHandshakeTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RecordingOptions _options;
    private readonly string _tempDir;

    public GuacamoleRecordingHandshakeTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(dbOptions);

        _tempDir = Path.Combine(Path.GetTempPath(), "so-rec-handshake-" + Guid.NewGuid().ToString("N"));
        _options = new RecordingOptions { LocalPath = _tempDir };
    }

    public void Dispose()
    {
        _db.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private static Connection BuildConnection(bool? vaultRecordingEnabled, RecordingOverride connectionOverride,
        bool? connectionIncludeKeys = null, bool? vaultIncludeKeys = null)
    {
        var vault = vaultRecordingEnabled is null && vaultIncludeKeys is null
            ? null
            : new ConnectionGroup
            {
                Id = Guid.NewGuid(),
                Name = "vault",
                RecordingEnabled = vaultRecordingEnabled ?? false,
                RecordingIncludeKeys = vaultIncludeKeys ?? false,
            };
        return new Connection
        {
            Id = Guid.NewGuid(),
            Name = "c",
            Protocol = "rdp",
            HostId = Guid.NewGuid(),
            ConnectionGroupId = vault?.Id,
            ConnectionGroup = vault,
            RecordingOverride = connectionOverride,
            RecordingIncludeKeys = connectionIncludeKeys,
        };
    }

    [Fact]
    public async Task PrepareRecordingCore_WhenDisabled_ReturnsOriginalOverridesAndWritesNothing()
    {
        var conn = BuildConnection(vaultRecordingEnabled: false, RecordingOverride.Inherit);
        var seedOverrides = new Dictionary<string, string> { ["password"] = "secret" };

        var result = await GuacamoleProxyService.PrepareRecordingCoreAsync(
            conn, Guid.NewGuid(), "sess-1", DateTime.UtcNow, "1.2.3.4",
            seedOverrides, _db, _options, NullLogger.Instance);

        Assert.Null(result.RecordingId);
        Assert.Null(result.TempPath);
        Assert.Same(seedOverrides, result.SettingsOverrides);
        Assert.Empty(await _db.Recordings.AsNoTracking().ToListAsync());
        Assert.Empty(await _db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PrepareRecordingCore_WhenForceOff_ReturnsOriginalOverridesEvenIfVaultEnabled()
    {
        var conn = BuildConnection(vaultRecordingEnabled: true, RecordingOverride.ForceOff);
        var result = await GuacamoleProxyService.PrepareRecordingCoreAsync(
            conn, Guid.NewGuid(), "sess-1", DateTime.UtcNow, "1.2.3.4",
            null, _db, _options, NullLogger.Instance);

        Assert.Null(result.RecordingId);
        Assert.Empty(await _db.Recordings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PrepareRecordingCore_WhenEnabledByVault_CreatesRecordingRowAndAuditLog_AndMergesOverrides()
    {
        var conn = BuildConnection(vaultRecordingEnabled: true, RecordingOverride.Inherit, vaultIncludeKeys: true);
        var userId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 7, 12, 14, 30, 0, DateTimeKind.Utc);

        var result = await GuacamoleProxyService.PrepareRecordingCoreAsync(
            conn, userId, "session-id-42", startedAt, "10.0.0.1",
            settingsOverrides: null, _db, _options, NullLogger.Instance);

        Assert.NotNull(result.RecordingId);
        Assert.NotNull(result.TempPath);
        Assert.Equal(Path.Combine(_tempDir, "session-id-42.guac"), result.TempPath);
        Assert.NotNull(result.SettingsOverrides);
        Assert.Equal(_tempDir, result.SettingsOverrides["recording-path"]);
        Assert.Equal("session-id-42.guac", result.SettingsOverrides["recording-name"]);
        Assert.Equal("true", result.SettingsOverrides["create-recording-path"]);
        Assert.Equal("true", result.SettingsOverrides["recording-include-keys"]);

        var rec = Assert.Single(await _db.Recordings.AsNoTracking().ToListAsync());
        Assert.Equal(result.RecordingId, rec.Id);
        Assert.Equal("session-id-42", rec.SessionId);
        Assert.Equal(conn.Id, rec.ConnectionId);
        Assert.Equal(userId, rec.UserId);
        Assert.Equal(startedAt, rec.StartedAt);
        Assert.Equal($"2026/07/{conn.Id}/session-id-42.guac", rec.StorageKey);
        Assert.True(rec.IncludeKeys);
        Assert.Equal(RecordingStatus.Recording, rec.Status);

        var audit = Assert.Single(await _db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal("recording.started", audit.Action);
        Assert.Equal("Recording", audit.ResourceType);
        Assert.Equal(rec.Id.ToString(), audit.ResourceId);
        Assert.Equal("10.0.0.1", audit.IpAddress);
    }

    [Fact]
    public async Task PrepareRecordingCore_WhenForceOn_CreatesRecordingEvenIfVaultDisabled()
    {
        var conn = BuildConnection(vaultRecordingEnabled: false, RecordingOverride.ForceOn,
            connectionIncludeKeys: false);

        var result = await GuacamoleProxyService.PrepareRecordingCoreAsync(
            conn, Guid.NewGuid(), "sess", DateTime.UtcNow, "1.2.3.4",
            settingsOverrides: null, _db, _options, NullLogger.Instance);

        Assert.NotNull(result.RecordingId);
        Assert.Equal("false", result.SettingsOverrides!["recording-include-keys"]);
        Assert.Single(await _db.Recordings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PrepareRecordingCore_MergesIntoExistingOverridesWithoutMutatingTheCaller()
    {
        var conn = BuildConnection(vaultRecordingEnabled: true, RecordingOverride.Inherit);
        var caller = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["password"] = "p" };

        var result = await GuacamoleProxyService.PrepareRecordingCoreAsync(
            conn, Guid.NewGuid(), "sess", DateTime.UtcNow, "ip",
            settingsOverrides: caller, _db, _options, NullLogger.Instance);

        Assert.NotSame(caller, result.SettingsOverrides);
        Assert.Equal("p", result.SettingsOverrides!["password"]);
        Assert.Contains("recording-path", result.SettingsOverrides);
        Assert.False(caller.ContainsKey("recording-path"), "caller dict must not be mutated");
    }

    [Fact]
    public async Task PrepareRecordingCore_WhenTempDirUnwritable_WritesSkippedAuditAndReturnsOriginal()
    {
        // Point at a path INSIDE an existing file — Directory.CreateDirectory will throw
        // IOException because the parent is not a directory. Portable across Linux + Windows.
        var blockingFile = Path.Combine(Path.GetTempPath(), "so-block-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(blockingFile, "x");
        try
        {
            var unwritable = new RecordingOptions { LocalPath = Path.Combine(blockingFile, "subdir") };
            var conn = BuildConnection(vaultRecordingEnabled: true, RecordingOverride.Inherit);
            var seed = new Dictionary<string, string> { ["k"] = "v" };

            var result = await GuacamoleProxyService.PrepareRecordingCoreAsync(
                conn, Guid.NewGuid(), "sess", DateTime.UtcNow, "ip",
                seed, _db, unwritable, NullLogger.Instance);

            Assert.Null(result.RecordingId);
            Assert.Null(result.TempPath);
            Assert.Same(seed, result.SettingsOverrides);

            var audit = Assert.Single(await _db.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Equal("recording.skipped", audit.Action);
            Assert.Equal("failure", audit.Outcome);
            using var doc = JsonDocument.Parse(audit.Details);
            Assert.Equal("temp_dir_unwritable", doc.RootElement.GetProperty("reason").GetString());
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }

    [Fact]
    public async Task WriteRecordingSkippedAuditCore_WritesAuditLog()
    {
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        await GuacamoleProxyService.WriteRecordingSkippedAuditCoreAsync(
            _db, userId, connectionId, "sess-99", "5.6.7.8", "test_reason", "test detail",
            NullLogger.Instance);

        var audit = Assert.Single(await _db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal("recording.skipped", audit.Action);
        Assert.Equal(connectionId.ToString(), audit.ResourceId);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal("5.6.7.8", audit.IpAddress);
        Assert.Equal("failure", audit.Outcome);

        using var doc = JsonDocument.Parse(audit.Details);
        Assert.Equal("sess-99", doc.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal("test_reason", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal("test detail", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task WriteRecordingSkippedAuditCore_OnDbException_LogsWarningButDoesNotThrow()
    {
        await _db.DisposeAsync(); // Force any DB operation to throw ObjectDisposedException.

        var ex = await Record.ExceptionAsync(() =>
            GuacamoleProxyService.WriteRecordingSkippedAuditCoreAsync(
                _db, Guid.NewGuid(), Guid.NewGuid(), "sess", "ip", "reason", "detail",
                NullLogger.Instance));

        // The method must swallow any failure so it never tears down the calling session flow.
        Assert.Null(ex);
    }
}
