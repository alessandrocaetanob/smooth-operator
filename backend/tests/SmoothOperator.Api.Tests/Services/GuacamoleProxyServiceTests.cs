using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Domain.Models;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Xunit;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using SmoothOperator.Application.Exceptions;

namespace SmoothOperator.Api.Tests.Services;

public class GuacamoleProxyServiceTests
{
    private readonly Mock<ILogger<GuacamoleProxyService>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<IAppMetrics> _metricsMock = new();
    private readonly Mock<ISecretProviderFactory> _secretProviderFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly IOptions<GuacdOptions> _options = Options.Create(new GuacdOptions { Host = "localhost", Port = 4822 });
    private readonly GuacamoleProxyService _service;

    public GuacamoleProxyServiceTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(dbContext);

        _service = new GuacamoleProxyService(
            _loggerMock.Object,
            _options,
            _scopeFactoryMock.Object,
            _encryptionServiceMock.Object,
            _redisMock.Object,
            _metricsMock.Object,
            _secretProviderFactoryMock.Object);
    }

    [Fact]
    public async Task IssueTicketAsync_PersistsTicketToRedisAndWritesAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var ip = "127.0.0.1";
        string? capturedRedisKey = null;
        _dbMock.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true)
            .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>(
                (k, v, t, w, f) => capturedRedisKey = k.ToString());

        // Act
        var ticket = await _service.IssueTicketAsync(userId, connectionId, ip);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(ticket));
        Assert.NotNull(capturedRedisKey);
        Assert.StartsWith("guac:ticket:", capturedRedisKey);

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "connection.ticket.issued");
        Assert.NotNull(log);
        Assert.Equal(connectionId.ToString(), log.ResourceId);
    }

    [Fact]
    public async Task ConsumeTicketAsync_ValidTicket_ReturnsUserId()
    {
        // Arrange
        var ticket = "test-ticket";
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var ip = "127.0.0.1";
        var payload = JsonSerializer.Serialize(new { UserId = userId, ConnectionId = connectionId, IpAddress = ip });
        _dbMock.Setup(db => db.StringGetDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(payload);

        // Act
        var result = await _service.ConsumeTicketAsync(ticket, connectionId, ip);

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task ConsumeTicketAsync_WhenTicketDoesNotMatch_ReturnsNull()
    {
        // Arrange
        var ticket = "test-ticket";
        var connectionId = Guid.NewGuid();
        var wrongConnectionId = Guid.NewGuid();
        var ip = "127.0.0.1";
        var payload = JsonSerializer.Serialize(new { UserId = Guid.NewGuid(), ConnectionId = wrongConnectionId, IpAddress = ip });
        _dbMock.Setup(db => db.StringGetDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(payload);

        // Act
        var result = await _service.ConsumeTicketAsync(ticket, connectionId, ip);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ConsumeTicketAsync_WhenIpMismatched_ReturnsNull()
    {
        // Arrange
        var ticket = "test-ticket";
        var connectionId = Guid.NewGuid();
        var storedIp = "127.0.0.1";
        var currentIp = "192.168.1.1";
        var payload = JsonSerializer.Serialize(new { UserId = Guid.NewGuid(), ConnectionId = connectionId, IpAddress = storedIp });
        _dbMock.Setup(db => db.StringGetDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(payload);

        // Act
        var result = await _service.ConsumeTicketAsync(ticket, connectionId, currentIp);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ConsumeTicketAsync_WhenDeserializationFails_ReturnsNull()
    {
        // Arrange
        var ticket = "test-ticket";
        _dbMock.Setup(db => db.StringGetDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync("invalid-json");

        // Act
        var result = await _service.ConsumeTicketAsync(ticket, Guid.NewGuid(), "127.0.0.1");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleWebSocketAsync_WhenUserInactive_ClosesWebSocket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var ip = "127.0.0.1";

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User { Id = userId, IsActive = false, Name = "inactive-user", Email = "test@test.com" });
        await dbContext.SaveChangesAsync();

        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act
        await _service.HandleWebSocketAsync(wsMock.Object, connectionId, userId, ip);

        // Assert
        wsMock.Verify(w => w.CloseAsync(
            System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation,
            "User not authorized",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleWebSocketAsync_WhenConnectionNotFound_ClosesWebSocket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var ip = "127.0.0.1";

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User { Id = userId, IsActive = true, Name = "active-user", Email = "test@test.com" });
        await dbContext.SaveChangesAsync();

        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act
        await _service.HandleWebSocketAsync(wsMock.Object, connectionId, userId, ip);

        // Assert
        wsMock.Verify(w => w.CloseAsync(
            System.Net.WebSockets.WebSocketCloseStatus.InvalidMessageType,
            "Connection not found",
            It.IsAny<CancellationToken>()), Times.Once);

        var log = await dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "connection.failed");
        Assert.NotNull(log);
    }

    [Fact]
    public async Task HandleWebSocketAsync_WhenHostUnreachable_ClosesWebSocket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var ip = "127.0.0.1";

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User { Id = userId, IsActive = true, Name = "active-user", Email = "test@test.com" });

        var host = new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "test-host", Address = "127.0.0.1" };
        dbContext.Hosts.Add(host);

        dbContext.Connections.Add(new Connection
        {
            Id = connectionId,
            HostId = hostId,
            Host = host,
            Protocol = "rdp",
            Settings = "{\"port\":\"59999\"}"
        });
        await dbContext.SaveChangesAsync();

        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        wsMock.Setup(w => w.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<System.Net.WebSockets.WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act - This will fail at ProbeHostAsync because nothing is listening on port 59999
        await _service.HandleWebSocketAsync(wsMock.Object, connectionId, userId, ip);

        // Assert
        wsMock.Verify(w => w.CloseAsync(
            System.Net.WebSockets.WebSocketCloseStatus.InternalServerError,
            It.Is<string>(s => s.Contains("not reachable")),
            It.IsAny<CancellationToken>()), Times.Once);

        var log = await dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "connection.host_unreachable");
        Assert.NotNull(log);
    }

    [Fact]
    public async Task HandleWebSocketAsync_WhenGuacdConnectionFails_DoesNotRecordSessionMetrics_AndCleansSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var ip = "127.0.0.1";

        // Reachable target endpoint so pre-flight probe succeeds.
        using var reachableListener = new TcpListener(IPAddress.Loopback, 0);
        reachableListener.Start();
        var reachablePort = ((IPEndPoint)reachableListener.LocalEndpoint).Port;

        var service = new GuacamoleProxyService(
            _loggerMock.Object,
            Options.Create(new GuacdOptions { Host = "invalid.invalid", Port = 4822 }),
            _scopeFactoryMock.Object,
            _encryptionServiceMock.Object,
            _redisMock.Object,
            _metricsMock.Object,
            _secretProviderFactoryMock.Object);

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User { Id = userId, IsActive = true, Name = "active-user", Email = "test@test.com" });

        var host = new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "test-host", Address = "127.0.0.1" };
        dbContext.Hosts.Add(host);

        dbContext.Connections.Add(new Connection
        {
            Id = connectionId,
            HostId = hostId,
            Host = host,
            Protocol = "rdp",
            Settings = $"{{\"port\":\"{reachablePort}\"}}"
        });
        await dbContext.SaveChangesAsync();

        string? deletedSessionKey = null;
        _dbMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true)
            .Callback<RedisKey, CommandFlags>((key, _) => deletedSessionKey = key.ToString());

        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        wsMock.Setup(w => w.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<System.Net.WebSockets.WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act
        await service.HandleWebSocketAsync(wsMock.Object, connectionId, userId, ip);

        // Assert
        Assert.NotNull(deletedSessionKey);
        Assert.StartsWith("session:", deletedSessionKey);
        _metricsMock.Verify(m => m.RecordConnectionStarted(), Times.Never);
        _metricsMock.Verify(m => m.RecordConnectionEnded(), Times.Never);
        wsMock.Verify(w => w.CloseAsync(
            System.Net.WebSockets.WebSocketCloseStatus.InternalServerError,
            It.Is<string>(s => s.Contains("Cannot reach Guacamole service")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConsumeTicketAsync_WithInvalidTicket_ReturnsNull(string ticket)
    {
        // Act
        var result = await _service.ConsumeTicketAsync(ticket, Guid.NewGuid(), "127.0.0.1");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task IssueTicketAsync_WithEmptyIp_SavesToRedis()
    {
        // Act
        var ticket = await _service.IssueTicketAsync(Guid.NewGuid(), Guid.NewGuid(), string.Empty);

        // Assert
        Assert.NotNull(ticket);
    }

    [Fact]
    public async Task HandleWebSocketAsync_WhenUserNotFound_ClosesWebSocket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act
        await _service.HandleWebSocketAsync(wsMock.Object, Guid.NewGuid(), userId, "127.0.0.1");

        // Assert
        wsMock.Verify(w => w.CloseAsync(
            System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation,
            "User not authorized",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleWebSocketAsync_WhenHostIsNull_ClosesWebSocket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User { Id = userId, IsActive = true, Name = "active-user", Email = "test@test.com" });
        dbContext.Connections.Add(new Connection { Id = connectionId, HostId = Guid.Empty, Host = null });
        await dbContext.SaveChangesAsync();

        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act
        await _service.HandleWebSocketAsync(wsMock.Object, connectionId, userId, "127.0.0.1");

        // Assert
        wsMock.Verify(w => w.CloseAsync(
            System.Net.WebSockets.WebSocketCloseStatus.InvalidMessageType,
            "Connection not found",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ssh", 22)]
    [InlineData("vnc", 5900)]
    [InlineData("telnet", 23)]
    [InlineData("rdp", 3389)]
    public async Task HandleWebSocketAsync_CheckDefaultPorts(string protocol, int expectedPort)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        using var scope = _scopeFactoryMock.Object.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User { Id = userId, IsActive = true, Name = "active-user", Email = "test@test.com" });

        // Using the IANA-reserved 'invalid.invalid' hostname for immediate DNS NXDOMAIN failure,
        // avoiding the 5-second TCP probe timeout that a non-routable IP like 192.0.2.1 would incur.
        var host = new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "test-host", Address = "invalid.invalid" };
        dbContext.Hosts.Add(host);

        dbContext.Connections.Add(new Connection
        {
            Id = connectionId,
            HostId = hostId,
            Host = host,
            Protocol = protocol,
            Settings = "{}" // No port override
        });
        await dbContext.SaveChangesAsync();

        var wsMock = new Mock<System.Net.WebSockets.WebSocket>();
        wsMock.Setup(w => w.State).Returns(System.Net.WebSockets.WebSocketState.Open);
        wsMock.Setup(w => w.CloseAsync(It.IsAny<System.Net.WebSockets.WebSocketCloseStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        wsMock.Setup(w => w.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<System.Net.WebSockets.WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // Act - It will fail at ProbeHostAsync but we want to see it use the correct port in the log/details
        await _service.HandleWebSocketAsync(wsMock.Object, connectionId, userId, "127.0.0.1");

        // Assert
        var log = await dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "connection.host_unreachable");
        Assert.NotNull(log);
        Assert.Contains($"\"port\":{expectedPort}", log!.Details);
    }
}

public class GuacamoleProxyServiceInternalTests
{
    private readonly Mock<ILogger<GuacamoleProxyService>> _loggerMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IAppMetrics> _metricsMock = new();
    private readonly Mock<ISecretProviderFactory> _secretProviderFactoryMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly IOptions<GuacdOptions> _options = Options.Create(new GuacdOptions { Host = "localhost", Port = 4822 });
    private readonly GuacamoleProxyService _service;
    private readonly AppDbContext _dbContext;

    public GuacamoleProxyServiceInternalTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _service = new GuacamoleProxyService(
            _loggerMock.Object,
            _options,
            _scopeFactoryMock.Object,
            _encryptionServiceMock.Object,
            _redisMock.Object,
            _metricsMock.Object,
            _secretProviderFactoryMock.Object);
    }

    private Task<string> InvokeResolveDecryptedSecretAsync(Credential credential)
    {
        var method = typeof(GuacamoleProxyService).GetMethod(
            "ResolveDecryptedSecretAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (Task<string>)method!.Invoke(_service, new object?[]
        {
            credential,
            _dbContext,
            Guid.NewGuid(),
            "127.0.0.1",
            CancellationToken.None
        })!;
    }

    [Fact]
    public async Task ResolveConnectionParametersAsync_WithExternalSecret_FetchesFromProvider()
    {
        // Arrange
        var provider = new SecretProvider { Id = Guid.NewGuid(), Name = "Vault" };
        var cred = new Credential
        {
            Id = Guid.NewGuid(),
            StorageMode = SecretStorageMode.External,
            SecretProvider = provider,
            ExternalSecretName = "my-secret",
            Username = "guac-user"
        };
        var connection = new Connection { Credential = cred, Protocol = "ssh" };

        var secretProviderMock = new Mock<ISecretProvider>();
        secretProviderMock.Setup(p => p.GetSecretAsync("my-secret", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("secret-password");

        _secretProviderFactoryMock.Setup(f => f.Create(provider)).Returns(secretProviderMock.Object);

        // Act
        var method = typeof(GuacamoleProxyService).GetMethod("ResolveConnectionParametersAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<List<string>>)method!.Invoke(_service, new object?[]
        {
            connection,
            new List<string> { "username", "password" },
            "VERSION_1_5_0",
            _dbContext,
            CancellationToken.None,
            Guid.NewGuid(),
            "127.0.0.1"
        })!;
        var result = await task;

        // Assert
        Assert.Equal("guac-user", result[0]);
        Assert.Equal("secret-password", result[1]);

        var log = await _dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "secret.fetched");
        Assert.NotNull(log);
    }

    [Fact]
    public void ParseSettings_HandlesValidJson()
    {
        // Arrange
        var json = "{\"port\":\"22\",\"ignore-cert\":true,\"null-val\":null}";

        // Act
        var method = typeof(GuacamoleProxyService).GetMethod("ParseSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (Dictionary<string, string>)method!.Invoke(null, new object?[] { json })!;

        // Assert
        Assert.Equal("22", result["port"]);
        Assert.Equal("true", result["ignore-cert"]);
        Assert.Equal(string.Empty, result["null-val"]);
    }

    [Fact]
    public void ParseSettings_HandlesMalformedJson_ReturnsEmpty()
    {
        // Arrange
        var json = "invalid-json";

        // Act
        var method = typeof(GuacamoleProxyService).GetMethod("ParseSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (Dictionary<string, string>)method!.Invoke(null, new object?[] { json })!;

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ResolveConnectionParametersAsync_SecretProviderException_Throws()
    {
        // Arrange
        var provider = new SecretProvider { Id = Guid.NewGuid(), Name = "Vault" };
        var cred = new Credential
        {
            Id = Guid.NewGuid(),
            StorageMode = SecretStorageMode.External,
            SecretProvider = provider,
            ExternalSecretName = "my-secret"
        };
        var connection = new Connection { Credential = cred, Protocol = "ssh" };

        var secretProviderMock = new Mock<ISecretProvider>();
        secretProviderMock.Setup(p => p.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new SecretProviderException("403", "Access Denied"));

        _secretProviderFactoryMock.Setup(f => f.Create(provider)).Returns(secretProviderMock.Object);

        // Act & Assert
        var method = typeof(GuacamoleProxyService).GetMethod("ResolveConnectionParametersAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<List<string>>)method!.Invoke(_service, new object?[] { connection, new List<string> { "password" }, "VERSION_1_5_0", _dbContext, CancellationToken.None, Guid.NewGuid(), "127.0.0.1" })!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => task);

        var log = await _dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "secret.fetched" && l.Outcome == "failure");
        Assert.NotNull(log);
        Assert.Contains("403", log!.Details);
    }

    [Fact]
    public async Task ResolveDecryptedSecretAsync_LocalSecretDecryptFailure_ReturnsEmpty()
    {
        // Arrange
        var cred = new Credential
        {
            Id = Guid.NewGuid(),
            StorageMode = SecretStorageMode.Local,
            EncryptedSecret = "encrypted-secret"
        };
        _encryptionServiceMock.Setup(e => e.Decrypt("encrypted-secret"))
            .Throws(new InvalidOperationException("decrypt failed"));

        // Act
        var result = await InvokeResolveDecryptedSecretAsync(cred);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ResolveDecryptedSecretAsync_LocalWithoutEncryptedSecret_ReturnsEmpty()
    {
        // Arrange
        var cred = new Credential
        {
            Id = Guid.NewGuid(),
            StorageMode = SecretStorageMode.Local,
            EncryptedSecret = string.Empty
        };

        // Act
        var result = await InvokeResolveDecryptedSecretAsync(cred);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ResolveDecryptedSecretAsync_ExternalProviderUnexpectedFailure_ThrowsVaultUnreachable()
    {
        // Arrange
        var provider = new SecretProvider { Id = Guid.NewGuid(), Name = "Vault" };
        var cred = new Credential
        {
            Id = Guid.NewGuid(),
            StorageMode = SecretStorageMode.External,
            SecretProvider = provider,
            SecretProviderId = provider.Id,
            ExternalSecretName = "my-secret"
        };

        var secretProviderMock = new Mock<ISecretProvider>();
        secretProviderMock
            .Setup(p => p.GetSecretAsync("my-secret", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("provider unavailable"));

        _secretProviderFactoryMock.Setup(f => f.Create(provider)).Returns(secretProviderMock.Object);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeResolveDecryptedSecretAsync(cred));

        // Assert
        Assert.Contains("Vault unreachable", ex.Message);
        var log = await _dbContext.AuditLogs.FirstOrDefaultAsync(l => l.Action == "secret.fetched" && l.Outcome == "failure");
        Assert.NotNull(log);
        Assert.Contains("vault_unreachable", log!.Details);
    }

    [Fact]
    public async Task ResolveConnectionParametersAsync_SshKeyAuth_ConfiguresCorrectly()
    {
        // Arrange
        var cred = new Credential
        {
            Id = Guid.NewGuid(),
            CredentialType = "private_key",
            EncryptedSecret = "encrypted-key"
        };
        var connection = new Connection { Credential = cred, Protocol = "ssh" };
        _encryptionServiceMock.Setup(e => e.Decrypt("encrypted-key")).Returns("pem-key-content");

        // Act
        var method = typeof(GuacamoleProxyService).GetMethod("ResolveConnectionParametersAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<List<string>>)method!.Invoke(_service, new object?[] { connection, new List<string> { "password", "private-key" }, "VERSION_1_5_0", _dbContext, CancellationToken.None, Guid.NewGuid(), "127.0.0.1" })!;
        var result = await task;

        // Assert
        Assert.Equal(string.Empty, result[0]); // password should be empty for key auth
        Assert.Equal("pem-key-content", result[1]); // private-key should have the PEM content
    }

    [Fact]
    public async Task ResolveConnectionParametersAsync_ProtocolVersionNegotiation_EchoesVersion()
    {
        // Arrange
        var connection = new Connection { Protocol = "rdp" };

        // Act
        var method = typeof(GuacamoleProxyService).GetMethod("ResolveConnectionParametersAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<List<string>>)method!.Invoke(_service, new object?[] { connection, new List<string> { "VERSION_1_5_0", "hostname" }, "VERSION_1_5_0", _dbContext, CancellationToken.None, Guid.NewGuid(), "127.0.0.1" })!;
        var result = await task;

        // Assert
        Assert.Equal("VERSION_1_5_0", result[0]);
    }

    [Fact]
    public async Task GuacInstructionReader_ReadAsync_ParsesCorrectly()
    {
        // Arrange
        var data = "4.test,2.ok;5.hello;";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        var readerType = typeof(GuacamoleProxyService).GetNestedType("GuacInstructionReader", System.Reflection.BindingFlags.NonPublic);
        var reader = Activator.CreateInstance(readerType!, new object?[] { stream });
        var readAsync = readerType!.GetMethod("ReadAsync");

        // Act
        var task1 = (Task<List<string>>)readAsync!.Invoke(reader, new object?[] { CancellationToken.None })!;
        var result1 = await task1;
        var task2 = (Task<List<string>>)readAsync!.Invoke(reader, new object?[] { CancellationToken.None })!;
        var result2 = await task2;

        // Assert
        Assert.Equal(new[] { "test", "ok" }, result1);
        Assert.Equal(new[] { "hello" }, result2);
    }

    [Fact]
    public async Task GuacInstructionReader_ReadRawAsync_ParsesCorrectly()
    {
        // Arrange
        var data = "4.test,2.ok;5.hello;";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        var readerType = typeof(GuacamoleProxyService).GetNestedType("GuacInstructionReader", System.Reflection.BindingFlags.NonPublic);
        var reader = Activator.CreateInstance(readerType!, new object?[] { stream });
        var readRawAsync = readerType!.GetMethod("ReadRawAsync");

        // Act
        var task1 = (Task<string>)readRawAsync!.Invoke(reader, new object?[] { CancellationToken.None })!;
        var result1 = await task1;

        // Assert
        Assert.Equal("4.test,2.ok;", result1);
    }

    [Fact]
    public async Task GuacInstructionReader_HandlesFragmentedData()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        var sequence = new List<string> { "4.te", "st;5.he", "llo;" };
        int callCount = 0;
        streamMock.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns<Memory<byte>, CancellationToken>((mem, ct) =>
            {
                if (callCount >= sequence.Count) return new ValueTask<int>(0);
                var bytes = Encoding.UTF8.GetBytes(sequence[callCount++]);
                bytes.CopyTo(mem.Span);
                return new ValueTask<int>(bytes.Length);
            });

        var readerType = typeof(GuacamoleProxyService).GetNestedType("GuacInstructionReader", System.Reflection.BindingFlags.NonPublic);
        var reader = Activator.CreateInstance(readerType!, new object?[] { streamMock.Object });
        var readAsync = readerType!.GetMethod("ReadAsync");

        // Act
        var result1 = await (Task<List<string>>?)readAsync?.Invoke(reader, new object?[] { CancellationToken.None })!;
        var result2 = await (Task<List<string>>?)readAsync?.Invoke(reader, new object?[] { CancellationToken.None })!;

        // Assert
        Assert.Equal(new[] { "test" }, result1);
        Assert.Equal(new[] { "hello" }, result2);
    }
}
