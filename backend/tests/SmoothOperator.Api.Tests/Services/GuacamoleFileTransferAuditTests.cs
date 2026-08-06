using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

/// <summary>
/// Tests the per-file audit trail: <c>GuacamoleProxyService</c> observing the raw
/// Guacamole instruction stream (get/body/blob/end/put/ack) and turning a completed
/// transfer into a queryable <c>AuditLog</c> row (action <c>session.file_transferred</c>)
/// plus a matching <c>WebhookDelivery</c>, per HYP-5's acceptance criteria.
/// </summary>
public sealed class GuacamoleFileTransferAuditTests : IDisposable
{
    // Every scope gets its own AppDbContext instance (mirroring real DI: DbContext is
    // NOT thread-safe, so a fire-and-forget background write and this test's own
    // polling reads must never share one instance). All instances point at the same
    // named in-memory database, which the InMemory provider supports safely.
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly List<AppDbContext> _createdContexts = [];
    private readonly FakeWebhookEndpointCache _endpointCache = new();
    private readonly GuacamoleProxyService _service;

    public GuacamoleFileTransferAuditTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(BuildScope);

        _service = new GuacamoleProxyService(
            NullLogger<GuacamoleProxyService>.Instance,
            Options.Create(new GuacdOptions { Host = "localhost", Port = 4822 }),
            scopeFactoryMock.Object,
            Options.Create(new RecordingOptions()),
            Options.Create(new FileTransferOptions()),
            new GuacamoleProxyDependencies(
                Mock.Of<IEncryptionService>(),
                Mock.Of<IConnectionMultiplexer>(),
                Mock.Of<IAppMetrics>(),
                Mock.Of<ISecretProviderFactory>()));
    }

    public void Dispose()
    {
        foreach (var db in _createdContexts) db.Dispose();
    }

    private AppDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        var db = new AppDbContext(options);
        lock (_createdContexts) _createdContexts.Add(db);
        return db;
    }

    private IServiceScope BuildScope()
    {
        var db = NewDbContext();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(db);
        spMock.Setup(sp => sp.GetService(typeof(IWebhookEnqueuer)))
            .Returns(new WebhookEnqueuer(db, _endpointCache));
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        return scopeMock.Object;
    }

    private static string Instr(params string[] elements)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < elements.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(elements[i].Length).Append('.').Append(elements[i]);
        }
        return sb.Append(';').ToString();
    }

    private static byte[] Payload(params string[] instructions)
        => System.Text.Encoding.UTF8.GetBytes(string.Concat(instructions));

    private static GuacamoleProxyService.GuacSessionRequest BuildRequest(
        FileTransferPolicy policy, Guid userId, Guid connectionId, string sessionId = "sess-1") => new()
        {
            WebSocket = WebSocket.CreateFromStream(Stream.Null, new WebSocketCreationOptions()),
            Connection = new Connection { Id = connectionId, Name = "c", Protocol = "rdp", HostId = Guid.NewGuid() },
            SessionId = sessionId,
            User = new User { Id = userId, Name = "u", Email = "u@x", IsActive = true },
            IpAddress = "10.0.0.9",
            DbContext = null!,
            RedisDb = null!,
            SettingsOverrides = null,
            FileTransferPolicy = policy,
        };

    private async Task<AuditLog> WaitForAuditLogAsync(string action, int expectedCount = 1)
    {
        // ObserveServerToClientFileTransfer queues the write via Task.Run; poll rather
        // than sleep a fixed duration. The budget is generous (not just enough for the
        // lightly-loaded case) because the whole suite runs many test classes in
        // parallel, and thread-pool contention can delay the background Task.Run well
        // past what a single-test run would need. Each poll uses its own fresh
        // DbContext instance so it never races the background write's own instance.
        for (var i = 0; i < 300; i++)
        {
            using var db = NewDbContext();
            var matches = await db.AuditLogs.AsNoTracking().Where(l => l.Action == action).ToListAsync();
            if (matches.Count >= expectedCount) return matches[0];
            await Task.Delay(20);
        }
        throw new TimeoutException($"No AuditLog with action '{action}' appeared within the timeout.");
    }

    [Fact]
    public async Task Download_BodyBlobEnd_WritesFileTransferredAuditWithDownloadDirection()
    {
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var req = BuildRequest(FileTransferPolicy.Both, userId, connectionId);

        // body,object,stream,mimetype,name; — guacd allocates stream 5 for "report.pdf".
        _service.ObserveServerToClientFileTransfer(req, Instr("body", "0", "5", "application/pdf", "report.pdf"));
        Assert.True(req.PendingDownloads.ContainsKey(5));

        // "SGVsbG8=" -> "Hello" (5 bytes), "V29ybGQ=" -> "World" (5 bytes).
        _service.ObserveServerToClientFileTransfer(req, Instr("blob", "5", "SGVsbG8="));
        _service.ObserveServerToClientFileTransfer(req, Instr("blob", "5", "V29ybGQ="));
        _service.ObserveServerToClientFileTransfer(req, Instr("end", "5"));

        Assert.False(req.PendingDownloads.ContainsKey(5));

        var audit = await WaitForAuditLogAsync("session.file_transferred");
        Assert.Equal(userId, audit.UserId);
        Assert.Equal(connectionId.ToString(), audit.ResourceId);
        Assert.Equal("success", audit.Outcome);
        using var doc = JsonDocument.Parse(audit.Details);
        Assert.Equal("download", doc.RootElement.GetProperty("direction").GetString());
        Assert.Equal("report.pdf", doc.RootElement.GetProperty("fileName").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("sizeBytes").GetInt64());
    }

    [Fact]
    public async Task DirectoryListing_BodyMimetype_IsNeverAudited()
    {
        var req = BuildRequest(FileTransferPolicy.Both, Guid.NewGuid(), Guid.NewGuid());

        _service.ObserveServerToClientFileTransfer(
            req, Instr("body", "0", "3", "application/vnd.glyptodon.guacamole.stream-index+json", "/"));
        Assert.Empty(req.PendingDownloads); // never tracked — browsing isn't a transfer

        _service.ObserveServerToClientFileTransfer(req, Instr("blob", "3", "e30="));
        _service.ObserveServerToClientFileTransfer(req, Instr("end", "3"));

        await Task.Delay(100); // give any (unwanted) fire-and-forget write a chance to land
        using var db = NewDbContext();
        Assert.Empty(await db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Upload_SuccessfulAck_NeverFinalizesTransfer()
    {
        // Regression test: a real upload sends one `ack` per ~6KB blob chunk, so
        // finalizing on the first successful ack (the old, buggy behavior) would
        // audit only the first chunk's byte count for any file bigger than that.
        // A successful ack must be pure flow control — no audit, no removal.
        var req = BuildRequest(FileTransferPolicy.Both, Guid.NewGuid(), Guid.NewGuid());
        req.PendingUploads[9] = new GuacamoleProxyService.PendingFileTransfer { Name = "notes.txt", Bytes = 12 };

        _service.ObserveServerToClientFileTransfer(req, Instr("ack", "9", "OK", "0"));

        await Task.Delay(100); // give any (unwanted) fire-and-forget write a chance to land
        Assert.True(req.PendingUploads.ContainsKey(9));
        using var db = NewDbContext();
        Assert.Empty(await db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Upload_MultipleChunks_FinalizesOnClientEndWithFullByteCount()
    {
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var req = BuildRequest(FileTransferPolicy.Both, userId, connectionId);

        // Client opens the upload stream and sends two chunks, each acked by guacd —
        // mirrors the real BlobWriter, which waits for an ack before sending the next
        // chunk and only calls sendEnd() once every chunk has been acked successfully.
        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("put", "0", "9", "text/plain", "big.txt")));

        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, Payload(Instr("blob", "9", "SGVsbG8="))); // "Hello" = 5 bytes
        _service.ObserveServerToClientFileTransfer(req, Instr("ack", "9", "OK", "0"));

        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, Payload(Instr("blob", "9", "V29ybGQ="))); // "World" = 5 bytes
        _service.ObserveServerToClientFileTransfer(req, Instr("ack", "9", "OK", "0"));

        GuacamoleProxyService.PendingFileTransfer? completed = null;
        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("end", "9")), transfer => completed = transfer);

        Assert.False(req.PendingUploads.ContainsKey(9));
        Assert.NotNull(completed);
        Assert.Equal(10, completed.Bytes);

        await _service.WriteFileTransferAuditAsync(req, "upload", completed.Name, completed.Bytes, "success", null);

        var audit = await WaitForAuditLogAsync("session.file_transferred");
        Assert.Equal("success", audit.Outcome);
        using var doc = JsonDocument.Parse(audit.Details);
        Assert.Equal("upload", doc.RootElement.GetProperty("direction").GetString());
        Assert.Equal("big.txt", doc.RootElement.GetProperty("fileName").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("sizeBytes").GetInt64());
    }

    [Fact]
    public async Task Upload_AckWithNonZeroStatus_AuditsAsFailure()
    {
        var req = BuildRequest(FileTransferPolicy.Both, Guid.NewGuid(), Guid.NewGuid());
        req.PendingUploads[2] = new GuacamoleProxyService.PendingFileTransfer { Name = "big.zip", Bytes = 40 };

        _service.ObserveServerToClientFileTransfer(req, Instr("ack", "2", "disk full", "515"));

        var audit = await WaitForAuditLogAsync("session.file_transferred");
        Assert.Equal("failure", audit.Outcome);
        using var doc = JsonDocument.Parse(audit.Details);
        Assert.Equal("515", doc.RootElement.GetProperty("failureCode").GetString());
    }

    [Fact]
    public async Task Disabled_ObserveServerToClientFileTransfer_NeverTracksOrAudits()
    {
        var req = BuildRequest(FileTransferPolicy.Disabled, Guid.NewGuid(), Guid.NewGuid());

        _service.ObserveServerToClientFileTransfer(req, Instr("body", "0", "5", "application/pdf", "x.pdf"));
        _service.ObserveServerToClientFileTransfer(req, Instr("blob", "5", "SGVsbG8="));
        _service.ObserveServerToClientFileTransfer(req, Instr("end", "5"));

        Assert.Empty(req.PendingDownloads);
        await Task.Delay(100);
        using var db = NewDbContext();
        Assert.Empty(await db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task WriteFileTransferAuditAsync_EnqueuesMatchingWebhookDelivery()
    {
        _endpointCache.Endpoints.Add(new EnabledWebhookEndpoint(Guid.NewGuid(), "session.*"));
        var req = BuildRequest(FileTransferPolicy.Both, Guid.NewGuid(), Guid.NewGuid());

        await _service.WriteFileTransferAuditAsync(req, "download", "file.txt", 42, "success", null);

        using var db = NewDbContext();
        var delivery = Assert.Single(await db.WebhookDeliveries.AsNoTracking().ToListAsync());
        Assert.Equal("session.file_transferred", delivery.EventType);
    }

    [Fact]
    public async Task WriteFileTransferAuditAsync_NoMatchingWebhook_EnqueuesNoDelivery()
    {
        _endpointCache.Endpoints.Add(new EnabledWebhookEndpoint(Guid.NewGuid(), "user.*"));
        var req = BuildRequest(FileTransferPolicy.Both, Guid.NewGuid(), Guid.NewGuid());

        await _service.WriteFileTransferAuditAsync(req, "upload", "file.txt", 42, "success", null);

        using var db = NewDbContext();
        Assert.Empty(await db.WebhookDeliveries.AsNoTracking().ToListAsync());
    }

    private sealed class FakeWebhookEndpointCache : IWebhookEndpointCache
    {
        public List<EnabledWebhookEndpoint> Endpoints { get; } = [];

        public Task<IReadOnlyList<EnabledWebhookEndpoint>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EnabledWebhookEndpoint>>(Endpoints);

        public void Invalidate() { }
    }
}
