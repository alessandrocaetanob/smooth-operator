using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.Connections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Linq;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Infrastructure.Services
{
    public partial class GuacamoleProxyService
    {
        private const string ResourceTypeConnection = "Connection";
        private const string TicketInvalidAction = "connection.ticket.invalid";

        private readonly ILogger<GuacamoleProxyService> _logger;
        private readonly string _guacdHost;
        private readonly int _guacdPort;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEncryptionService _encryptionService;
        private readonly IConnectionMultiplexer _redis;
        private readonly IAppMetrics _metrics;
        private readonly ISecretProviderFactory _secretProviderFactory;
        private readonly RecordingOptions _recordingOptions;
        private readonly FileTransferOptions _fileTransferOptions;

        private static readonly TimeSpan TicketTtl = TimeSpan.FromSeconds(30);

        public GuacamoleProxyService(
            ILogger<GuacamoleProxyService> logger,
            IOptions<GuacdOptions> guacdOptions,
            IServiceScopeFactory scopeFactory,
            IOptions<RecordingOptions> recordingOptions,
            IOptions<FileTransferOptions> fileTransferOptions,
            GuacamoleProxyDependencies dependencies)
        {
            _logger = logger;
            _guacdHost = guacdOptions.Value.Host;
            _guacdPort = guacdOptions.Value.Port;
            _scopeFactory = scopeFactory;
            _encryptionService = dependencies.Encryption;
            _redis = dependencies.Redis;
            _metrics = dependencies.Metrics;
            _secretProviderFactory = dependencies.SecretProviderFactory;
            _recordingOptions = recordingOptions.Value;
            _fileTransferOptions = fileTransferOptions.Value;
        }

        // ---- Ticket lifecycle (REST issue / WS consume) ------------------------------

        public async Task<string> IssueTicketAsync(
            Guid userId,
            Guid connectionId,
            string ipAddress,
            IDictionary<string, string>? sessionSettingsOverrides = null)
        {
            var ticket = Guid.NewGuid().ToString("N");
            var key = TicketKey(ticket);
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(new TicketPayload
            {
                UserId = userId,
                ConnectionId = connectionId,
                IpAddress = ipAddress ?? string.Empty,
                SessionSettingsOverrides = sessionSettingsOverrides == null
                    ? null
                    : new Dictionary<string, string>(sessionSettingsOverrides, StringComparer.OrdinalIgnoreCase)
            });
            await db.StringSetAsync(key, payload, TicketTtl);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.ticket.issued",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = "{}",
                IpAddress = ipAddress ?? string.Empty
            });
            await dbContext.SaveChangesAsync();

            return ticket;
        }

        // Atomically consume a ticket. Returns the userId on success, null on
        // missing/expired/mismatched ticket. Uses GETDEL so a concurrent reader
        // can't replay the ticket.
        public async Task<TicketPayload?> ConsumeTicketAsync(string ticket, Guid connectionId, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ticket)) return null;
            var key = TicketKey(ticket);
            var db = _redis.GetDatabase();

            var raw = await db.StringGetDeleteAsync(key);
            if (raw.IsNullOrEmpty)
            {
                await LogTicketEventAsync(null, connectionId, TicketInvalidAction, ipAddress, "missing_or_expired");
                return null;
            }

            TicketPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<TicketPayload>((string)raw!);
            }
            catch
            {
                await LogTicketEventAsync(null, connectionId, TicketInvalidAction, ipAddress, "deserialize_failed");
                return null;
            }

            if (payload == null || payload.ConnectionId != connectionId)
            {
                await LogTicketEventAsync(payload?.UserId, connectionId, TicketInvalidAction, ipAddress, "connection_mismatch");
                return null;
            }

            // IP binding — mismatched IPs are suspicious. We log and refuse.
            // Allow empty stored or current IP (reverse proxy edge cases).
            if (!string.IsNullOrEmpty(payload.IpAddress)
                && !string.IsNullOrEmpty(ipAddress)
                && !string.Equals(payload.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase))
            {
                await LogTicketEventAsync(payload.UserId, connectionId, TicketInvalidAction, ipAddress, "ip_mismatch");
                return null;
            }

            await LogTicketEventAsync(payload.UserId, connectionId, "connection.ticket.consumed", ipAddress, null);
            return payload;
        }

        private static string TicketKey(string ticket) => $"guac:ticket:{ticket}";

        private async Task LogTicketEventAsync(Guid? userId, Guid connectionId, string action, string? ipAddress, string? reason)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = action,
                    ResourceType = ResourceTypeConnection,
                    ResourceId = connectionId.ToString(),
                    Details = reason == null ? "{}" : $"{{\"reason\":\"{reason}\"}}",
                    IpAddress = ipAddress ?? string.Empty
                });
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write ticket audit log {Action}", action);
            }
        }

        public sealed class TicketPayload
        {
            public Guid UserId { get; set; }
            public Guid ConnectionId { get; set; }
            public string IpAddress { get; set; } = string.Empty;
            public Dictionary<string, string>? SessionSettingsOverrides { get; set; }
        }

        // ---- WebSocket proxy ---------------------------------------------------------

        public async Task HandleWebSocketAsync(
            WebSocket webSocket,
            Guid connectionId,
            Guid userId,
            string ipAddress,
            IDictionary<string, string>? sessionSettingsOverrides = null)
        {
            var sessionId = Guid.NewGuid().ToString();

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbUser = await LoadAuthorizedUserAsync(dbContext, userId);
            if (dbUser == null)
            {
                _logger.LogWarning("User {UserId} not found or inactive. Connection denied.", userId);
                await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "User not authorized", CancellationToken.None);
                return;
            }

            var connection = await LoadConnectionAsync(dbContext, connectionId);
            if (connection == null || connection.Host == null)
            {
                await HandleMissingConnectionAsync(webSocket, dbContext, dbUser.Id, connectionId, ipAddress);
                return;
            }

            var (targetHost, targetPort) = ResolveTargetEndpoint(connection);
            if (!await ProbeWithTimeoutAsync(targetHost, targetPort, TimeSpan.FromSeconds(5)))
            {
                await HandleUnreachableHostAsync(webSocket, dbContext, dbUser.Id, connectionId, ipAddress, targetHost, targetPort);
                return;
            }

            await RecordConnectionStartedAuditAsync(dbContext, dbUser.Id, connectionId, ipAddress, connection, sessionId);

            var db = _redis.GetDatabase();
            await RegisterSessionAsync(db, sessionId, dbUser.Id, connectionId);

            await RunGuacamoleSessionAsync(new GuacSessionRequest
            {
                WebSocket = webSocket,
                Connection = connection,
                SessionId = sessionId,
                User = dbUser,
                IpAddress = ipAddress,
                DbContext = dbContext,
                RedisDb = db,
                SettingsOverrides = sessionSettingsOverrides,
            });
        }

        /// <summary>
        /// Mutable per-session state. <see cref="LastActivityAt"/> is bumped after every
        /// user-initiated payload reaches guacd; the watchdog reads it to enforce idle
        /// timeouts. Class (not record) so the watchdog and the relay loop see the same
        /// mutable values.
        /// </summary>
        internal sealed class GuacSessionRequest
        {
            public required WebSocket WebSocket { get; init; }
            public required Connection Connection { get; init; }
            public required string SessionId { get; init; }
            public required User User { get; init; }
            public required string IpAddress { get; init; }
            public required AppDbContext DbContext { get; init; }
            public required IDatabase RedisDb { get; init; }
            public required IDictionary<string, string>? SettingsOverrides { get; init; }

            public DateTime SessionStartedAt { get; init; } = DateTime.UtcNow;
            public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

            /// <summary>0 = disabled.</summary>
            public int IdleTimeoutMinutes { get; set; }

            /// <summary>0 = unlimited.</summary>
            public int MaxSessionMinutes { get; set; }

            /// <summary>
            /// Set during handshake when the session is being recorded. Drives the
            /// post-session upload trigger and the audit-log enrichment on session-end.
            /// </summary>
            public Guid? RecordingId { get; set; }
            public string? RecordingTempPath { get; set; }

            /// <summary>Effective file-transfer policy resolved for this session (vault default + per-connection override).</summary>
            public Domain.Enums.FileTransferPolicy FileTransferPolicy { get; set; } = Domain.Enums.FileTransferPolicy.Disabled;

            /// <summary>Per-session RDP drive directory, set when RDP file transfer is enabled; removed when the session ends.</summary>
            public string? FileTransferDriveDir { get; set; }

            /// <summary>
            /// Streams guacd allocated in response to a client <c>get</c> (download) request,
            /// keyed by stream index. Written/read from both relay directions, hence concurrent.
            /// </summary>
            public ConcurrentDictionary<int, PendingFileTransfer> PendingDownloads { get; } = new();

            /// <summary>Streams the client allocated via <c>put</c> (upload) requests, keyed by stream index.</summary>
            public ConcurrentDictionary<int, PendingFileTransfer> PendingUploads { get; } = new();
        }

        /// <summary>Tracks an in-flight file-transfer stream until its terminating instruction arrives.</summary>
        internal sealed class PendingFileTransfer
        {
            public required string Name { get; init; }
            public long Bytes { get; set; }
        }

        private static Task<User?> LoadAuthorizedUserAsync(AppDbContext dbContext, Guid userId) =>
            dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        private static Task<Connection?> LoadConnectionAsync(AppDbContext dbContext, Guid connectionId) =>
            dbContext.Connections
                .Include(c => c.Host)
                .Include(c => c.Credential).ThenInclude(cr => cr!.SecretProvider)
                .Include(c => c.Users)
                .Include(c => c.ConnectionGroup)
                .FirstOrDefaultAsync(c => c.Id == connectionId);

        private async Task HandleMissingConnectionAsync(
            WebSocket webSocket, AppDbContext dbContext, Guid userId, Guid connectionId, string ipAddress)
        {
            _logger.LogWarning("Connection {ConnectionId} not found or has no host.", connectionId);

            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.failed",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = "{\"reason\":\"connection_not_found\"}",
                IpAddress = ipAddress
            });
            await dbContext.SaveChangesAsync();

            await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Connection not found", CancellationToken.None);
        }

        private static (string host, int port) ResolveTargetEndpoint(Connection connection)
        {
            var defaultPort = (connection.Protocol ?? "rdp").ToLowerInvariant() switch
            {
                "ssh" => 22,
                "vnc" => 5900,
                "telnet" => 23,
                _ => 3389
            };
            var targetHost = connection.Host!.Address;
            var targetPort = int.TryParse(
                ConnectionSettingsParser.Parse(connection.Settings).GetValueOrDefault("port"),
                out var parsedPort) ? parsedPort : defaultPort;
            return (targetHost, targetPort);
        }

        private static async Task<bool> ProbeWithTimeoutAsync(string host, int port, TimeSpan timeout)
        {
            using var probeCts = new CancellationTokenSource(timeout);
            return await ProbeHostAsync(host, port, probeCts.Token);
        }

        private async Task HandleUnreachableHostAsync(
            WebSocket webSocket, AppDbContext dbContext, Guid userId, Guid connectionId, string ipAddress,
            string targetHost, int targetPort)
        {
            var unreachableMsg = $"Host {targetHost}:{targetPort} is not reachable";
            _logger.LogWarning("Pre-flight check failed for connection {ConnectionId}: {Message}", connectionId, unreachableMsg);

            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.host_unreachable",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = $"{{\"host\":{JsonSerializer.Serialize(targetHost)},\"port\":{targetPort}}}",
                IpAddress = ipAddress
            });
            await dbContext.SaveChangesAsync();

            if (webSocket.State == WebSocketState.Open)
            {
                await TrySendGuacErrorAsync(webSocket, unreachableMsg, GuacStatus.UpstreamNotFound);
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, unreachableMsg[..Math.Min(unreachableMsg.Length, 120)], CancellationToken.None);
            }
        }

        private static async Task RecordConnectionStartedAuditAsync(
            AppDbContext dbContext, Guid userId, Guid connectionId, string ipAddress, Connection connection, string sessionId)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.started",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = JsonSerializer.Serialize(new
                {
                    host = connection.Host!.Name,
                    protocol = connection.Protocol,
                    sessionId
                }),
                IpAddress = ipAddress
            });
            await dbContext.SaveChangesAsync();
        }

        private static async Task RegisterSessionAsync(IDatabase db, string sessionId, Guid userId, Guid connectionId)
        {
            await db.HashSetAsync($"session:{sessionId}", new HashEntry[]
            {
                new HashEntry("UserId", userId.ToString()),
                new HashEntry("ConnectionId", connectionId.ToString()),
                new HashEntry("StartTime", DateTime.UtcNow.ToString("O"))
            });
            await db.KeyExpireAsync($"session:{sessionId}", TimeSpan.FromHours(24));
        }

        private async Task RunGuacamoleSessionAsync(GuacSessionRequest req)
        {
            using var tcpClient = new TcpClient();
            var connectionSuccessful = false;
            var sessionMetricRecorded = false;
            string? failureReason = null;
            try
            {
                await tcpClient.ConnectAsync(_guacdHost, _guacdPort);
                using var networkStream = tcpClient.GetStream();
                var reader = new GuacInstructionReader(networkStream);

                // Decide if this session is being recorded (vault default + per-connection override).
                // When yes: creates a Recording row, sets req.RecordingId/RecordingTempPath, and
                // returns the recording-* params that guacd needs in its handshake.
                var handshakeOverrides = await PrepareRecordingAsync(req);
                handshakeOverrides = await PrepareFileTransferAsync(req, handshakeOverrides);

                await PerformGuacamoleHandshakeAsync(
                    networkStream,
                    reader,
                    req.Connection,
                    req.DbContext,
                    req.User,
                    req.IpAddress,
                    handshakeOverrides);

                _metrics.RecordConnectionStarted();
                sessionMetricRecorded = true;
                connectionSuccessful = true;

                // Load timeout settings once at session start. Settings rarely change mid-session,
                // and the watchdog uses these values to decide when to terminate.
                var settings = await req.DbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
                req.IdleTimeoutMinutes = settings?.IdleTimeoutMinutes ?? 0;
                req.MaxSessionMinutes = settings?.MaxSessionMinutes ?? 0;

                await RunBidirectionalProxyAsync(req, reader, networkStream, msg =>
                {
                    failureReason ??= msg;
                    _logger.LogWarning("guacd reported error for connection {ConnectionId}: {Error}", req.Connection.Id, msg);
                });
            }
            catch (SocketException ex)
            {
                failureReason = $"Cannot reach Guacamole service ({_guacdHost}:{_guacdPort}): {ex.Message}";
                _logger.LogError(ex, "Network error connecting to guacd for connection {ConnectionId}", req.Connection.Id);
                await LogConnectionErrorAsync(req.DbContext, req.User.Id, req.Connection.Id, req.IpAddress, "network_error", ex.Message);
            }
            catch (Exception ex)
            {
                failureReason = $"Session error: {ex.Message}";
                _logger.LogError(ex, "Error during Guacamole session for connection {ConnectionId}", req.Connection.Id);
                await LogConnectionErrorAsync(req.DbContext, req.User.Id, req.Connection.Id, req.IpAddress, "session_error", ex.Message);
            }
            finally
            {
                await req.RedisDb.KeyDeleteAsync($"session:{req.SessionId}");

                if (connectionSuccessful)
                {
                    await LogConnectionEndedAsync(req.DbContext, req.User.Id, req.Connection.Id, req.IpAddress, req.SessionId, req.RecordingId);
                }

                if (req.RecordingId.HasValue && !string.IsNullOrEmpty(req.RecordingTempPath))
                {
                    // True fire-and-forget — uploads can run hundreds of megabytes and we
                    // don't want session teardown / WebSocket close to wait on cloud I/O.
                    // The upload service owns its own scope and updates Recording.Status.
                    _ = Task.Run(() => TriggerRecordingUploadAsync(req.RecordingId.Value, req.RecordingTempPath));
                }

                if (!string.IsNullOrEmpty(req.FileTransferDriveDir))
                {
                    CleanupFileTransferDriveDir(req.FileTransferDriveDir, req.SessionId);
                }

                if (sessionMetricRecorded)
                {
                    _metrics.RecordConnectionEnded();
                }

                await CloseGuacSessionAsync(req.WebSocket, failureReason);
                tcpClient.Close();
            }
        }

        // ---- Session-recording wiring -----------------------------------------------

        /// <summary>
        /// Outcome of the recording handshake step: which Recording row was created (if any),
        /// where the temp file will live, and the merged Guacamole handshake overrides.
        /// </summary>
        internal sealed record RecordingHandshakeResult(
            Guid? RecordingId,
            string? TempPath,
            IDictionary<string, string>? SettingsOverrides);

        /// <summary>
        /// If the resolved Connection (or its parent Vault) has recording enabled, create
        /// a <c>Recording</c> row and return the handshake overrides with the recording-*
        /// Guacamole params merged in. Otherwise returns the caller's overrides unchanged.
        /// </summary>
        private async Task<IDictionary<string, string>?> PrepareRecordingAsync(GuacSessionRequest req)
        {
            var result = await PrepareRecordingCoreAsync(
                req.Connection,
                req.User.Id,
                req.SessionId,
                req.SessionStartedAt,
                req.IpAddress,
                req.SettingsOverrides,
                req.DbContext,
                _recordingOptions,
                _logger);

            if (result.RecordingId.HasValue)
            {
                req.RecordingId = result.RecordingId;
                req.RecordingTempPath = result.TempPath;
            }
            return result.SettingsOverrides;
        }

        /// <summary>
        /// Pure-input variant of <see cref="PrepareRecordingAsync"/> with no WebSocket/Redis
        /// dependencies — used by unit tests so the recording decision tree can be exercised
        /// without standing up the full session pipeline.
        /// </summary>
        // The shared docker volume must be writable by BOTH this container (app uid)
        // AND the guacd container (uid 1000). Permissions are set at image-build time
        // in the backend Dockerfile (mkdir + chmod 0777) so the named volume inherits
        // a world-writable mode on first creation. cap_drop:[ALL] strips CAP_FOWNER,
        // so we cannot chmod it from .NET at runtime — recreate the volume
        // (`docker compose down -v`) if you see "permission denied" here.
        internal static async Task<RecordingHandshakeResult> PrepareRecordingCoreAsync(
            Connection connection,
            Guid userId,
            string sessionId,
            DateTime sessionStartedAt,
            string ipAddress,
            IDictionary<string, string>? settingsOverrides,
            AppDbContext db,
            RecordingOptions recordingOptions,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var (enabled, includeKeys) = ResolveRecordingEffectiveFlags(connection);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Recording evaluated for session {SessionId} (connection {ConnectionId}): vault={VaultEnabled}, override={Override}, effective={Enabled}, includeKeys={IncludeKeys}",
                    sessionId,
                    connection.Id,
                    connection.ConnectionGroup?.RecordingEnabled ?? false,
                    connection.RecordingOverride,
                    enabled,
                    includeKeys);
            }

            if (!enabled) return new RecordingHandshakeResult(null, null, settingsOverrides);

            var tempDir = recordingOptions.LocalPath;
            try
            {
                Directory.CreateDirectory(tempDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Recording enabled but temp dir '{TempDir}' is not writable; session will proceed UNRECORDED. Recreate the recordings_temp volume to pick up the Dockerfile permissions.",
                    tempDir);
                await WriteRecordingSkippedAuditCoreAsync(db, userId, connection.Id, sessionId, ipAddress, "temp_dir_unwritable", ex.Message, logger, cancellationToken);
                return new RecordingHandshakeResult(null, null, settingsOverrides);
            }

            var fileName = $"{sessionId}.guac";
            var tempPath = Path.Combine(tempDir, fileName);
            var storageKey = $"{sessionStartedAt:yyyy}/{sessionStartedAt:MM}/{connection.Id}/{fileName}";

            var recording = new Domain.Models.Recording
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ConnectionId = connection.Id,
                UserId = userId,
                StartedAt = sessionStartedAt,
                StorageKey = storageKey,
                StorageType = Domain.Enums.RecordingStorageType.Local, // settled when upload finalises
                IncludeKeys = includeKeys,
                Status = Domain.Enums.RecordingStatus.Recording,
            };
            db.Recordings.Add(recording);

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "recording.started",
                ResourceType = "Recording",
                ResourceId = recording.Id.ToString(),
                Details = JsonSerializer.Serialize(new
                {
                    sessionId,
                    connectionId = connection.Id,
                    storageKey,
                    includeKeys,
                }),
                IpAddress = ipAddress,
            });
            await db.SaveChangesAsync(cancellationToken);

            // Merge recording-* params into a fresh dict so we don't mutate the caller's overrides.
            var overrides = settingsOverrides == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(settingsOverrides, StringComparer.OrdinalIgnoreCase);
            overrides["recording-path"] = tempDir;
            overrides["recording-name"] = fileName;
            overrides["create-recording-path"] = "true";
            overrides["recording-include-keys"] = includeKeys ? "true" : "false";
            return new RecordingHandshakeResult(recording.Id, tempPath, overrides);
        }

        internal static (bool Enabled, bool IncludeKeys) ResolveRecordingEffectiveFlags(Connection connection)
        {
            var vault = connection.ConnectionGroup;
            var vaultEnabled = vault?.RecordingEnabled ?? false;
            var enabled = connection.RecordingOverride switch
            {
                Domain.Enums.RecordingOverride.ForceOn => true,
                Domain.Enums.RecordingOverride.ForceOff => false,
                _ => vaultEnabled,
            };
            var includeKeys = connection.RecordingIncludeKeys ?? vault?.RecordingIncludeKeys ?? false;
            return (enabled, includeKeys);
        }

        internal static async Task WriteRecordingSkippedAuditCoreAsync(
            AppDbContext db,
            Guid userId,
            Guid connectionId,
            string sessionId,
            string ipAddress,
            string reason,
            string detail,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "recording.skipped",
                    ResourceType = ResourceTypeConnection,
                    ResourceId = connectionId.ToString(),
                    Details = JsonSerializer.Serialize(new { sessionId, reason, detail }),
                    IpAddress = ipAddress,
                    Outcome = "failure",
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write recording.skipped audit log for session {SessionId}", sessionId);
            }
        }

        private async Task TriggerRecordingUploadAsync(Guid recordingId, string tempPath)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uploader = scope.ServiceProvider.GetRequiredService<IRecordingUploadService>();
                await uploader.UploadAndFinaliseAsync(recordingId, tempPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recording upload trigger failed for recording {RecordingId}", recordingId);
            }
        }

        // ---- File-transfer wiring (SFTP for SSH, drive redirect for RDP) ------------

        /// <summary>
        /// Outcome of the file-transfer handshake step: the resolved policy, the RDP
        /// drive directory created (if any), and the merged Guacamole handshake overrides.
        /// </summary>
        internal sealed record FileTransferHandshakeResult(
            Domain.Enums.FileTransferPolicy Policy,
            string? DriveDir,
            IDictionary<string, string>? SettingsOverrides);

        /// <summary>
        /// Resolves the connection's effective file-transfer policy and, when not
        /// Disabled, merges the guacd params that enable it: <c>enable-sftp</c> for SSH
        /// (guacd reuses the primary SSH connection's own credentials — no extra
        /// directory needed), or <c>enable-drive</c>/<c>drive-path</c> for RDP (which
        /// does need a directory, created per-session under <see cref="FileTransferOptions.DrivePath"/>
        /// and removed when the session ends). VNC/telnet have no Guacamole file-transfer
        /// support, so the policy is resolved but no params are sent.
        /// </summary>
        private async Task<IDictionary<string, string>?> PrepareFileTransferAsync(
            GuacSessionRequest req, IDictionary<string, string>? overrides)
        {
            var result = await PrepareFileTransferCoreAsync(
                req.Connection,
                req.User.Id,
                req.SessionId,
                req.IpAddress,
                overrides,
                req.DbContext,
                _fileTransferOptions,
                _logger);

            req.FileTransferPolicy = result.Policy;
            req.FileTransferDriveDir = result.DriveDir;
            return result.SettingsOverrides;
        }

        /// <summary>
        /// Pure-input variant of <see cref="PrepareFileTransferAsync"/> with no WebSocket
        /// dependency — used by unit tests so the policy → guacd-params decision tree can
        /// be exercised without standing up the full session pipeline.
        /// </summary>
        internal static async Task<FileTransferHandshakeResult> PrepareFileTransferCoreAsync(
            Connection connection,
            Guid userId,
            string sessionId,
            string ipAddress,
            IDictionary<string, string>? settingsOverrides,
            AppDbContext db,
            FileTransferOptions fileTransferOptions,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var policy = FileTransferPolicyResolver.Resolve(connection);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "File transfer evaluated for session {SessionId} (connection {ConnectionId}): vault={VaultPolicy}, override={Override}, effective={Policy}",
                    sessionId,
                    connection.Id,
                    connection.ConnectionGroup?.FileTransferPolicy,
                    connection.FileTransferPolicyOverride,
                    policy);
            }

            if (policy == Domain.Enums.FileTransferPolicy.Disabled)
                return new FileTransferHandshakeResult(policy, null, settingsOverrides);

            var protocol = (connection.Protocol ?? "rdp").ToLowerInvariant();
            var merged = settingsOverrides == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(settingsOverrides, StringComparer.OrdinalIgnoreCase);

            if (protocol == "ssh")
            {
                merged["enable-sftp"] = "true";
                merged["sftp-disable-upload"] = policy == Domain.Enums.FileTransferPolicy.DownloadOnly ? "true" : "false";
                merged["sftp-disable-download"] = policy == Domain.Enums.FileTransferPolicy.UploadOnly ? "true" : "false";
                return new FileTransferHandshakeResult(policy, null, merged);
            }

            if (protocol != "rdp")
            {
                // VNC/telnet: Guacamole has no file-transfer support for these protocols.
                return new FileTransferHandshakeResult(policy, null, settingsOverrides);
            }

            var driveDir = Path.Combine(fileTransferOptions.DrivePath, sessionId);
            try
            {
                Directory.CreateDirectory(driveDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "File transfer enabled but drive dir '{DriveDir}' is not writable; session will proceed WITHOUT file transfer. Recreate the file_transfer_temp volume to pick up the Dockerfile permissions.",
                    driveDir);
                await WriteFileTransferSkippedAuditCoreAsync(db, userId, connection.Id, sessionId, ipAddress, "drive_dir_unwritable", ex.Message, logger, cancellationToken);
                return new FileTransferHandshakeResult(policy, null, settingsOverrides);
            }

            merged["enable-drive"] = "true";
            merged["drive-path"] = driveDir;
            merged["create-drive-path"] = "true";
            merged["drive-name"] = "Smooth Operator";
            merged["disable-upload"] = policy == Domain.Enums.FileTransferPolicy.DownloadOnly ? "true" : "false";
            merged["disable-download"] = policy == Domain.Enums.FileTransferPolicy.UploadOnly ? "true" : "false";
            return new FileTransferHandshakeResult(policy, driveDir, merged);
        }

        internal static async Task WriteFileTransferSkippedAuditCoreAsync(
            AppDbContext db,
            Guid userId,
            Guid connectionId,
            string sessionId,
            string ipAddress,
            string reason,
            string detail,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "file_transfer.skipped",
                    ResourceType = ResourceTypeConnection,
                    ResourceId = connectionId.ToString(),
                    Details = JsonSerializer.Serialize(new { sessionId, reason, detail }),
                    IpAddress = ipAddress,
                    Outcome = "failure",
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write file_transfer.skipped audit log for session {SessionId}", sessionId);
            }
        }

        private void CleanupFileTransferDriveDir(string driveDir, string sessionId)
        {
            try
            {
                if (Directory.Exists(driveDir)) Directory.Delete(driveDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up file-transfer drive dir for session {SessionId}", sessionId);
            }
        }

        private async Task PerformGuacamoleHandshakeAsync(
            NetworkStream networkStream,
            GuacInstructionReader reader,
            Connection connection,
            AppDbContext dbContext,
            User dbUser,
            string ipAddress,
            IDictionary<string, string>? sessionSettingsOverrides)
        {
            string protocol = (connection.Protocol ?? "rdp").ToLowerInvariant();
            await SendGuacMessage(networkStream, BuildGuacInstruction("select", protocol));

            var (paramNames, serverVersion) = await ReadHandshakeArgsAsync(reader);

            var paramValues = await ResolveConnectionParametersAsync(new ConnectionParametersRequest(
                connection,
                paramNames,
                serverVersion,
                dbContext,
                dbUser.Id,
                ipAddress,
                sessionSettingsOverrides,
                CancellationToken.None));

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "guacd handshake for {Protocol}: server={ServerVersion}, sending {ValueCount} connect values for {NameCount} arg names",
                    protocol, serverVersion, paramValues.Count, paramNames.Count);

            await SendHandshakeDefaultsAsync(networkStream, serverVersion);

            // connect — values must be in the exact order guacd asked for in `args`.
            var connectArgs = new List<string> { "connect" };
            connectArgs.AddRange(paramValues);
            await SendGuacMessage(networkStream, BuildGuacInstruction(connectArgs.ToArray()));
        }

        private static async Task<(List<string> paramNames, string serverVersion)> ReadHandshakeArgsAsync(GuacInstructionReader reader)
        {
            // guacd replies with `args` listing all parameter names that the
            // protocol supports, in the order it expects them on `connect`.
            var argsInstruction = await reader.ReadAsync(CancellationToken.None);
            if (argsInstruction == null || argsInstruction.Count == 0 || argsInstruction[0] != "args")
            {
                throw new InvalidOperationException(
                    $"Unexpected guacd handshake reply: {(argsInstruction == null ? "<null>" : string.Join(",", argsInstruction))}");
            }

            // First element is "args". Per the Apache reference client
            // (ConfiguredGuacamoleSocket.java), if the SECOND element looks
            // like a protocol version token (e.g. "VERSION_1_5_0"), it is
            // NOT skipped — it is treated as the first parameter name and
            // the client must echo it back as the first connect VALUE so
            // both sides agree on the protocol revision.
            var paramNames = argsInstruction.Skip(1).ToList();
            var serverVersion = paramNames.Count > 0 && paramNames[0].StartsWith("VERSION_", StringComparison.Ordinal)
                ? paramNames[0]
                : "VERSION_1_0_0";
            return (paramNames, serverVersion);
        }

        private static async Task SendHandshakeDefaultsAsync(NetworkStream networkStream, string serverVersion)
        {
            // size / audio / video / image — sensible defaults; the client may
            // also send a `size` instruction later to renegotiate.
            await SendGuacMessage(networkStream, BuildGuacInstruction("size", "1024", "768", "96"));
            await SendGuacMessage(networkStream, BuildGuacInstruction("audio", "audio/L16"));
            await SendGuacMessage(networkStream, BuildGuacInstruction("video"));
            await SendGuacMessage(networkStream, BuildGuacInstruction("image", "image/png", "image/jpeg"));
            // 1.5+ added optional `timezone` and `name` handshake instructions.
            if (string.Compare(serverVersion, "VERSION_1_1_0", StringComparison.Ordinal) >= 0)
            {
                await SendGuacMessage(networkStream, BuildGuacInstruction("timezone", "UTC"));
                await SendGuacMessage(networkStream, BuildGuacInstruction("name", "smooth-operator"));
            }
        }

        private async Task RunBidirectionalProxyAsync(
            GuacSessionRequest req,
            GuacInstructionReader reader,
            NetworkStream networkStream,
            Action<string> onGuacError)
        {
            // Bidirectional proxy with proper instruction framing on the guacd→ws side.
            using var watchdogCts = new CancellationTokenSource();
            var receiveTask = ProxyGuacdToWebSocket(req, reader, req.WebSocket, onGuacError);
            var sendTask = ProxyWebSocketToGuacd(req, req.WebSocket, networkStream, () =>
            {
                req.LastActivityAt = DateTime.UtcNow;
                // Traces genuine user input that resets the idle clock (Debug — off by default).
                LogUserActivityDetected(req.SessionId);
            });
            var watchdogTask = RunIdleAndMaxSessionWatchdogAsync(req, watchdogCts.Token);

            // Session liveness is defined by the relay tasks only. The watchdog is a
            // supervisor: when a timeout fires it closes the WebSocket, which makes the
            // relay tasks complete here. It must NOT be part of this WhenAny — when no
            // timeout is configured the watchdog returns synchronously, so its task is
            // already complete and the relay would be torn down the instant it starts.
            await Task.WhenAny(receiveTask, sendTask);
            // Cancel the watchdog promptly so its Task.Delay loop exits cleanly even
            // when the relay finished first.
            await watchdogCts.CancelAsync();
            // Ensure the watchdog has fully completed before returning so the caller's
            // finally block doesn't race the watchdog's DbContext use on shutdown.
            try { await watchdogTask; }
            catch (OperationCanceledException) { /* expected on cancellation */ }
        }

        /// <summary>
        /// Pure decision function: given the relevant timestamps + configured
        /// thresholds, returns the close reason if a timeout is breached, else null.
        /// Extracted so the policy is unit-testable without standing up a real
        /// WebSocket + guacd round-trip.
        /// </summary>
        internal static string? EvaluateSessionTimeout(
            DateTime sessionStartedAt,
            DateTime lastActivityAt,
            DateTime now,
            int idleTimeoutMinutes,
            int maxSessionMinutes)
        {
            if (idleTimeoutMinutes > 0 &&
                now - lastActivityAt > TimeSpan.FromMinutes(idleTimeoutMinutes))
            {
                return "idle-timeout";
            }
            if (maxSessionMinutes > 0 &&
                now - sessionStartedAt > TimeSpan.FromMinutes(maxSessionMinutes))
            {
                return "max-session-exceeded";
            }
            return null;
        }

        /// <summary>
        /// Background sentinel that closes the WebSocket when idle/max-session limits
        /// are exceeded. Only runs when at least one timeout is configured.
        /// </summary>
        private async Task RunIdleAndMaxSessionWatchdogAsync(GuacSessionRequest req, CancellationToken ct)
        {
            if (req.IdleTimeoutMinutes <= 0 && req.MaxSessionMinutes <= 0)
            {
                LogWatchdogDisabled(req.SessionId, req.IdleTimeoutMinutes, req.MaxSessionMinutes);
                return;
            }

            LogWatchdogStarted(req.SessionId, req.IdleTimeoutMinutes, req.MaxSessionMinutes);

            var pollInterval = TimeSpan.FromSeconds(15);
            while (!ct.IsCancellationRequested && req.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    await Task.Delay(pollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var reason = EvaluateSessionTimeout(
                    sessionStartedAt: req.SessionStartedAt,
                    lastActivityAt: req.LastActivityAt,
                    now: now,
                    idleTimeoutMinutes: req.IdleTimeoutMinutes,
                    maxSessionMinutes: req.MaxSessionMinutes);

                // Per-poll trace of the idle/session clocks (Debug — off by default).
                LogWatchdogPoll(
                    req.SessionId,
                    (now - req.LastActivityAt).TotalSeconds, req.IdleTimeoutMinutes,
                    (now - req.SessionStartedAt).TotalSeconds, req.MaxSessionMinutes,
                    reason is null ? string.Empty : $" -> closing: {reason}");

                if (reason is null) continue;

                await CloseSessionForTimeoutAsync(req, reason, now);
                return;
            }
        }

        /// <summary>
        /// Writes the timeout audit record and closes the WebSocket. Extracted from
        /// the watchdog loop so <see cref="RunIdleAndMaxSessionWatchdogAsync"/> stays
        /// within its cognitive-complexity budget.
        /// </summary>
        private async Task CloseSessionForTimeoutAsync(GuacSessionRequest req, string reason, DateTime now)
        {
            LogClosingSession(req.SessionId, req.Connection.Id, reason);

            // Audit on a fresh DbContext from the scope factory — the request-scoped
            // req.DbContext is also used by the relay tasks and DbContext is NOT
            // thread-safe (Gemini PR #127 review).
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bgDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                bgDb.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = req.User.Id,
                    Action = "session.timeout_closed",
                    ResourceType = ResourceTypeConnection,
                    ResourceId = req.Connection.Id.ToString(),
                    Details = JsonSerializer.Serialize(new
                    {
                        sessionId = req.SessionId,
                        reason,
                        idleMinutes = (int)(now - req.LastActivityAt).TotalMinutes,
                        totalMinutes = (int)(now - req.SessionStartedAt).TotalMinutes,
                    }),
                    IpAddress = req.IpAddress,
                });
                await bgDb.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* swallow — best-effort audit */ }

            if (req.WebSocket.State != WebSocketState.Open) return;
            try
            {
                await req.WebSocket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation, reason, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The relay tasks still observe the close on their next read;
                // log so a failed close handshake is diagnosable.
                _logger.LogWarning(ex,
                    "Session {SessionId}: watchdog CloseAsync threw while closing for {Reason}",
                    req.SessionId, reason);
            }
        }

        // ---- Source-generated session-timeout logging -------------------------------
        // [LoggerMessage] partial methods: no params-array allocation or value-type
        // boxing at the call site, with a built-in IsEnabled gate (clears CA1873).

        // Trace, not Debug — fires on every user input (key/mouse/touch/clipboard),
        // so a single interactive session emits hundreds per second. Lift to Debug
        // only when diagnosing the idle watchdog itself.
        [LoggerMessage(Level = LogLevel.Trace,
            Message = "Session {SessionId}: user activity detected — idle clock reset")]
        partial void LogUserActivityDetected(string sessionId);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Session {SessionId}: idle/max-session watchdog disabled — no timeout configured (idle={Idle}, max={Max})")]
        partial void LogWatchdogDisabled(string sessionId, int idle, int max);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Session {SessionId}: idle/max-session watchdog started (idle={Idle}min, max={Max}min)")]
        partial void LogWatchdogStarted(string sessionId, int idle, int max);

        // Trace, not Debug — fires every 15 s per session for every active connection.
        [LoggerMessage(Level = LogLevel.Trace,
            Message = "Session {SessionId}: watchdog poll — idle {IdleSeconds:F0}s (limit {IdleLimit}min), session {SessionSeconds:F0}s (limit {MaxLimit}min){Outcome}")]
        partial void LogWatchdogPoll(
            string sessionId, double idleSeconds, int idleLimit,
            double sessionSeconds, int maxLimit, string outcome);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Closing session {SessionId} for connection {ConnectionId}: {Reason}")]
        partial void LogClosingSession(string sessionId, Guid connectionId, string reason);

        private static async Task LogConnectionErrorAsync(
            AppDbContext dbContext, Guid userId, Guid connectionId, string ipAddress, string errorType, string message)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.error",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = $"{{\"error\":\"{errorType}\",\"message\":{JsonSerializer.Serialize(message)}}}",
                IpAddress = ipAddress
            });
            await dbContext.SaveChangesAsync();
        }

        private static async Task LogConnectionEndedAsync(
            AppDbContext dbContext, Guid userId, Guid connectionId, string ipAddress, string sessionId, Guid? recordingId)
        {
            var endTime = DateTime.UtcNow;
            object details = recordingId.HasValue
                ? new { sessionId, endTime = endTime.ToString("O"), recordingId = recordingId.Value }
                : (object)new { sessionId, endTime = endTime.ToString("O") };

            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.ended",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = JsonSerializer.Serialize(details),
                IpAddress = ipAddress
            });
            await dbContext.SaveChangesAsync();
        }

        private async Task CloseGuacSessionAsync(WebSocket webSocket, string? failureReason)
        {
            if (failureReason != null && webSocket.State == WebSocketState.Open)
                await TrySendGuacErrorAsync(webSocket, failureReason, GuacStatus.UpstreamError);

            if (webSocket.State != WebSocketState.Open) return;

            var status = failureReason != null
                ? WebSocketCloseStatus.InternalServerError
                : WebSocketCloseStatus.NormalClosure;
            var reason = failureReason ?? "Session ended";
            // WebSocket close reason has a 123-byte cap.
            if (reason.Length > 120) reason = reason.Substring(0, 120);
            await webSocket.CloseAsync(status, reason, CancellationToken.None);
        }

        // Guacamole protocol error codes (subset). See:
        // https://guacamole.apache.org/doc/gug/protocol-reference.html
        private static class GuacStatus
        {
            public const int UpstreamError = 0x0203;
            public const int UpstreamNotFound = 0x0205;
        }

        // Returns true when the TCP port is open (or if the connection attempt
        // times out in a way that still indicates the host is alive). Returns
        // false when the host is unreachable, refused, or the probe times out.
        private static async Task<bool> ProbeHostAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                using var probe = new TcpClient();
                await probe.ConnectAsync(host, port, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task TrySendGuacErrorAsync(WebSocket webSocket, string message, int code)
        {
            try
            {
                var instr = BuildGuacInstruction("error", message, code.ToString());
                var bytes = Encoding.UTF8.GetBytes(instr);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send Guacamole error instruction to client");
            }
        }

        // ---- Connect-parameter resolution -------------------------------------------

        // Build the value list for guacd's `connect` instruction in the order
        // requested by its `args` reply. Parameters we don't recognise are sent
        // as empty strings (guacd treats those as "use default").
        private sealed record ConnectionParametersRequest(
            Connection Connection,
            IReadOnlyList<string> ParamNames,
            string ServerVersion,
            AppDbContext DbContext,
            Guid UserId,
            string IpAddress,
            IDictionary<string, string>? SettingsOverrides,
            CancellationToken CancellationToken);

        private async Task<List<string>> ResolveConnectionParametersAsync(ConnectionParametersRequest req)
        {
            var protocol = (req.Connection.Protocol ?? "rdp").ToLowerInvariant();
            var hostname = req.Connection.Host?.Address ?? string.Empty;
            var defaultPort = protocol switch
            {
                "ssh" => "22",
                "vnc" => "5900",
                "telnet" => "23",
                _ => "3389"
            };
            var username = req.Connection.Credential?.Username ?? string.Empty;
            var credentialType = (req.Connection.Credential?.CredentialType ?? "password").ToLowerInvariant();
            var isKeyAuth = credentialType is "private_key" or "ssh_key" or "key";
            var decryptedSecret = req.Connection.Credential != null
                ? await ResolveDecryptedSecretAsync(req.Connection.Credential, req.DbContext, req.UserId, req.IpAddress, req.CancellationToken)
                : string.Empty;

            // For key auth the decrypted secret is the PEM private key, not a password.
            var password = isKeyAuth ? string.Empty : decryptedSecret;
            var privateKey = isKeyAuth ? decryptedSecret : string.Empty;

            // Optional per-connection settings JSON (e.g. {"domain":"corp","ignore-cert":"true",
            // "passphrase":"..."}). `passphrase` falls through to the override path below.
            Dictionary<string, string> overrides = ConnectionSettingsParser.Parse(req.Connection.Settings);
            if (req.SettingsOverrides != null)
            {
                foreach (var pair in req.SettingsOverrides.Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)))
                {
                    overrides[pair.Key.Trim()] = pair.Value.Trim();
                }
            }

            var ctx = new GuacConnectionContext(overrides, hostname, defaultPort, username, password, privateKey, protocol);

            var values = new List<string>(req.ParamNames.Count);
            foreach (var name in req.ParamNames)
            {
                // Echo the protocol version back when guacd advertised it as the
                // first arg name; this is how the version negotiation handshake
                // completes (see Apache ConfiguredGuacamoleSocket reference impl).
                if (name.StartsWith("VERSION_", StringComparison.Ordinal))
                {
                    values.Add(req.ServerVersion);
                    continue;
                }
                values.Add(ResolveConnectionParameter(name, ctx));
            }
            return values;
        }

        private static string ResolveConnectionParameter(string name, GuacConnectionContext ctx)
        {
            if (ctx.Overrides.TryGetValue(name, out var v)) return v ?? string.Empty;
            return name switch
            {
                "hostname" => ctx.Hostname,
                "port" => ctx.DefaultPort,
                "username" => ctx.Username,
                "password" => ctx.Password,
                // SSH key auth — guacd's SSH plugin reads the PEM body from
                // `private-key` and an optional `passphrase`. When the
                // credential is key-based we route the decrypted secret here
                // and leave `password` empty so guacd doesn't fall back to
                // password/keyboard-interactive auth.
                "private-key" => ctx.PrivateKey,
                "ignore-cert" => ctx.Protocol == "rdp" ? "true" : string.Empty,
                "security" => ctx.Protocol == "rdp" ? "any" : string.Empty,
                "resize-method" => ctx.Protocol == "rdp" ? "display-update" : string.Empty,
                _ => string.Empty
            };
        }

        // ---- Instruction framing & I/O ----------------------------------------------

        private static string BuildGuacInstruction(params string[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string arg = args[i] ?? string.Empty;
                // Guacamole length is in UTF-16 code units, not bytes.
                sb.Append(arg.Length);
                sb.Append('.');
                sb.Append(arg);
            }
            sb.Append(';');
            return sb.ToString();
        }

        private static async Task SendGuacMessage(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data.AsMemory());
            await stream.FlushAsync();
        }

        // Reads complete `LENGTH.VALUE,LENGTH.VALUE,...;` instructions off a stream.
        private sealed class GuacInstructionReader
        {
            private readonly Stream _stream;
            private readonly byte[] _buf = new byte[16 * 1024];
            // _charBuf is the same size as _buf: UTF-8 decoding can produce at most
            // 1 UTF-16 char per input byte (multi-byte sequences always map to fewer
            // chars than bytes, including 4-byte sequences that produce 2 chars from
            // 4 bytes), so the output can never exceed the byte count.
            private readonly char[] _charBuf = new char[16 * 1024];
            private readonly StringBuilder _pending = new();
            private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

            public GuacInstructionReader(Stream stream) { _stream = stream; }

            // Returns the parsed elements of the next complete instruction, or
            // null when the underlying stream closes cleanly.
            public async Task<List<string>?> ReadAsync(CancellationToken ct)
            {
                while (true)
                {
                    var parsed = TryParseOne(out var consumedChars);
                    if (parsed != null)
                    {
                        _pending.Remove(0, consumedChars);
                        return parsed;
                    }

                    int n = await _stream.ReadAsync(_buf.AsMemory(0, _buf.Length), ct);
                    if (n == 0) return null;
                    int charCount = _decoder.GetChars(_buf, 0, n, _charBuf, 0);
                    _pending.Append(_charBuf, 0, charCount);
                }
            }

            // Returns the raw text (including the trailing `;`) of the next
            // complete instruction, suitable for forwarding verbatim.
            public async Task<string?> ReadRawAsync(CancellationToken ct)
            {
                while (true)
                {
                    var raw = TryParseRawOne(out var consumedChars);
                    if (raw != null)
                    {
                        _pending.Remove(0, consumedChars);
                        return raw;
                    }

                    int n = await _stream.ReadAsync(_buf.AsMemory(0, _buf.Length), ct);
                    if (n == 0) return null;
                    int charCount = _decoder.GetChars(_buf, 0, n, _charBuf, 0);
                    _pending.Append(_charBuf, 0, charCount);
                }
            }

            private List<string>? TryParseOne(out int consumedChars)
            {
                consumedChars = 0;
                var list = new List<string>();
                int i = 0;
                while (true)
                {
                    int dot = IndexOf('.', i);
                    if (dot < 0) return null;

                    if (!int.TryParse(_pending.ToString(i, dot - i), out var len) || len < 0)
                    {
                        // Malformed; drain the buffer to recover.
                        consumedChars = _pending.Length;
                        return [];
                    }

                    int valStart = dot + 1;
                    int valEnd = valStart + len;
                    if (_pending.Length < valEnd + 1) return null;

                    list.Add(_pending.ToString(valStart, len));
                    char terminator = _pending[valEnd];
                    if (terminator == ';')
                    {
                        consumedChars = valEnd + 1;
                        return list;
                    }
                    if (terminator != ',') return null;
                    i = valEnd + 1;
                }
            }

            private string? TryParseRawOne(out int consumedChars)
            {
                consumedChars = 0;
                int i = 0;
                while (true)
                {
                    int dot = IndexOf('.', i);
                    if (dot < 0) return null;
                    if (!int.TryParse(_pending.ToString(i, dot - i), out var len) || len < 0)
                    {
                        consumedChars = _pending.Length;
                        return string.Empty;
                    }
                    int valEnd = dot + 1 + len;
                    if (_pending.Length < valEnd + 1) return null;
                    char terminator = _pending[valEnd];
                    if (terminator == ';')
                    {
                        consumedChars = valEnd + 1;
                        return _pending.ToString(0, consumedChars);
                    }
                    if (terminator != ',') return null;
                    i = valEnd + 1;
                }
            }

            private int IndexOf(char c, int start)
            {
                for (int i = start; i < _pending.Length; i++)
                    if (_pending[i] == c) return i;
                return -1;
            }
        }

        private async Task ProxyGuacdToWebSocket(GuacSessionRequest req, GuacInstructionReader reader, WebSocket webSocket, Action<string> onGuacError)
        {
            var ct = CancellationToken.None;
            while (webSocket.State == WebSocketState.Open)
            {
                var raw = await reader.ReadRawAsync(ct);
                if (raw == null) break;
                if (raw.Length == 0) continue;

                // Inspect for guacd `error` instruction so we can surface it
                // as the WS close reason if the session ends right after.
                // Format: `5.error,LEN.MESSAGE,LEN.CODE;`
                TryReportGuacError(raw, onGuacError);
                ObserveServerToClientFileTransfer(req, raw);

                var bytes = Encoding.UTF8.GetBytes(raw);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    ct);
            }
        }

        // Parses a single LENGTH.VALUE,...; instruction string into its elements.
        // Returns null if malformed.
        private static List<string>? ParseInstruction(string raw)
        {
            var list = new List<string>();
            int i = 0;
            while (i < raw.Length)
            {
                int dot = raw.IndexOf('.', i);
                if (dot < 0) return null;
                if (!int.TryParse(raw.AsSpan(i, dot - i), out var len) || len < 0) return null;
                int valStart = dot + 1;
                int valEnd = valStart + len;
                if (raw.Length < valEnd + 1) return null;
                list.Add(raw.Substring(valStart, len));
                char terminator = raw[valEnd];
                if (terminator == ';') return list;
                if (terminator != ',') return null;
                i = valEnd + 1;
            }
            return null;
        }

        // Client-to-guacd instructions that represent genuine human interaction.
        // Only these reset the idle-timeout clock. Everything else the Guacamole
        // client emits on its own must NOT count as activity, or the idle timeout
        // never fires. That excludes the sync heartbeat, the tunnel keep-alive nop
        // and ping, stream ack instructions, and crucially the size instruction.
        // The size instruction is display-geometry negotiation, not input: the
        // client emits it on display init, on every browser-window resize, and
        // for terminal protocols such as SSH (whose display quantizes to whole
        // character cells) in a continuous feedback loop with guacd's own size
        // replies, firing many times per second while the user does nothing.
        private static readonly string[] UserActivityOpcodes = ["key", "mouse", "touch", "clipboard"];

        /// <summary>
        /// True when a client→guacd payload contains at least one user-input
        /// instruction (see <see cref="UserActivityOpcodes"/>). A single payload
        /// may carry several concatenated <c>LENGTH.opcode,…;</c> instructions.
        /// </summary>
        internal static bool ContainsUserActivity(byte[] payload)
        {
            var text = Encoding.UTF8.GetString(payload);
            int i = 0;
            while (i < text.Length)
            {
                int dot = text.IndexOf('.', i);
                if (dot < 0) break;
                if (!int.TryParse(text.AsSpan(i, dot - i), out var len) || len < 0) break;

                int opStart = dot + 1;
                if (opStart + len > text.Length) break;

                var opcode = text.AsSpan(opStart, len);
                foreach (var activity in UserActivityOpcodes)
                {
                    if (opcode.SequenceEqual(activity)) return true;
                }

                // Skip to the end of this instruction (the next ';') and continue.
                int semicolon = text.IndexOf(';', opStart + len);
                if (semicolon < 0) break;
                i = semicolon + 1;
            }
            return false;
        }

        private static async Task ProxyWebSocketToGuacd(GuacSessionRequest req, WebSocket webSocket, NetworkStream guacdStream, Action onActivity)
        {
            var buffer = new byte[16 * 1024];
            using var ms = new MemoryStream();
            while (webSocket.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    await ms.WriteAsync(buffer.AsMemory(0, result.Count));
                } while (!result.EndOfMessage);

                var payload = ms.ToArray();
                if (payload.Length == 0) continue;
                await guacdStream.WriteAsync(payload.AsMemory());
                await guacdStream.FlushAsync();
                // Only genuine user input resets the idle clock — keepalive
                // traffic (sync/nop/ping) must not keep an idle session alive.
                if (ContainsUserActivity(payload)) onActivity();
                ObserveClientToServerFileTransfer(req, payload);
            }
        }

        private sealed record GuacConnectionContext(
            Dictionary<string, string> Overrides,
            string Hostname,
            string DefaultPort,
            string Username,
            string Password,
            string PrivateKey,
            string Protocol);

        private async Task<string> ResolveDecryptedSecretAsync(
            Credential cred,
            AppDbContext dbContext,
            Guid userId,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            if (UsesExternalSecretProvider(cred))
            {
                return await FetchExternalSecretAsync(cred, dbContext, userId, ipAddress, cancellationToken);
            }

            if (!string.IsNullOrEmpty(cred.EncryptedSecret))
            {
                return TryDecryptLocalSecret(cred.EncryptedSecret);
            }

            return string.Empty;
        }

        private static bool UsesExternalSecretProvider(Credential cred) =>
            cred.StorageMode == Domain.Models.SecretStorageMode.External
            && cred.SecretProvider != null
            && !string.IsNullOrEmpty(cred.ExternalSecretName);

        private async Task<string> FetchExternalSecretAsync(
            Credential cred,
            AppDbContext dbContext,
            Guid userId,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            try
            {
                var secretProvider = _secretProviderFactory.Create(cred.SecretProvider!);
                var secret = await secretProvider.GetSecretAsync(
                    cred.ExternalSecretName!, cred.ExternalSecretVersion, cancellationToken);

                await WriteSecretAuditAsync(dbContext, cred, userId, ipAddress, success: true, errorCode: null, cancellationToken);
                return secret;
            }
            catch (SecretProviderException ex)
            {
                _logger.LogError(ex, "Secret provider error for credential {CredentialId}: [{ErrorCode}] {Message}", cred.Id, ex.ErrorCode, ex.Message);
                await WriteSecretAuditAsync(dbContext, cred, userId, ipAddress, success: false, errorCode: ex.ErrorCode, cancellationToken);
                throw new InvalidOperationException(ex.Message, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch secret from vault for credential {CredentialId}", cred.Id);
                await WriteSecretAuditAsync(dbContext, cred, userId, ipAddress, success: false, errorCode: "vault_unreachable", cancellationToken);
                throw new InvalidOperationException("Vault unreachable — cannot retrieve credential. Check secret provider configuration.", ex);
            }
        }

        private string TryDecryptLocalSecret(string encryptedSecret)
        {
            try
            {
                return _encryptionService.Decrypt(encryptedSecret);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt credential for connection");
                return string.Empty;
            }
        }

        private static async Task WriteSecretAuditAsync(
            AppDbContext dbContext,
            Credential cred,
            Guid userId,
            string ipAddress,
            bool success,
            string? errorCode,
            CancellationToken cancellationToken)
        {
            var details = success
                ? JsonSerializer.Serialize(new
                {
                    credentialId = cred.Id,
                    providerId = cred.SecretProviderId,
                    secretName = cred.ExternalSecretName,
                    success = true
                })
                : JsonSerializer.Serialize(new
                {
                    credentialId = cred.Id,
                    providerId = cred.SecretProviderId,
                    secretName = cred.ExternalSecretName,
                    success = false,
                    error = errorCode
                });

            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IpAddress = ipAddress,
                Action = "secret.fetched",
                ResourceType = "Credential",
                ResourceId = cred.Id.ToString(),
                Details = details
            };
            if (!success) log.Outcome = "failure";

            dbContext.AuditLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static void TryReportGuacError(string raw, Action<string> onGuacError)
        {
            if (raw.Length <= 7 || !raw.StartsWith("5.error,", StringComparison.Ordinal))
                return;
            var parsed = ParseInstruction(raw);
            if (parsed != null && parsed.Count >= 2)
            {
                var msg = parsed[1];
                var code = parsed.Count >= 3 ? parsed[2] : "?";
                onGuacError($"guacd: {msg} (code {code})");
            }
        }

        // ---- File-transfer stream observation & per-file audit -----------------------
        //
        // Guacamole tunnels file transfer through the SAME instruction stream as
        // everything else (mouse/keys/screen updates): guacd exposes an RDP drive or
        // SSH SFTP session as a `filesystem` object, and the browser reads/writes it
        // with `get`/`put`/`body`/`blob`/`ack`/`end` — see
        // https://guacamole.apache.org/doc/gug/protocol-reference.html. There is no
        // separate REST endpoint for file transfer; this is the only place a completed
        // transfer (and its name/size/direction) can be observed for the audit trail.
        //
        // `blob`/`end` also carry ordinary screen-update tile data, at high frequency,
        // so these observers must stay cheap for the common (non-file) case: a literal
        // prefix check, then — only on a match — a lean field extractor that never
        // materializes the (potentially large, base64) payload unless the stream index
        // is one we're actually tracking.

        private const string DirectoryListingMimetype = "application/vnd.glyptodon.guacamole.stream-index+json";

        // guacd -> client direction (ProxyGuacdToWebSocket).
        internal void ObserveServerToClientFileTransfer(GuacSessionRequest req, string raw)
        {
            if (req.FileTransferPolicy == Domain.Enums.FileTransferPolicy.Disabled) return;

            if (raw.StartsWith("4.body,", StringComparison.Ordinal))
            {
                HandleFileTransferBody(req, raw);
            }
            else if (raw.StartsWith("4.blob,", StringComparison.Ordinal))
            {
                if (req.PendingDownloads.IsEmpty) return;
                if (!TryReadStreamAndDataField(raw, 0, out var streamIndex, out var dataStart, out var dataLen)) return;
                if (req.PendingDownloads.TryGetValue(streamIndex, out var transfer))
                {
                    transfer.Bytes += DecodedBase64Length(raw.AsSpan(dataStart, dataLen));
                }
            }
            else if (raw.StartsWith("3.end,", StringComparison.Ordinal))
            {
                if (req.PendingDownloads.IsEmpty) return;
                var streamIndex = ReadSingleStreamIndex(raw, 0);
                if (streamIndex >= 0 && req.PendingDownloads.TryRemove(streamIndex, out var transfer))
                {
                    QueueFileTransferAudit(req, "download", transfer.Name, transfer.Bytes);
                }
            }
            else if (raw.StartsWith("3.ack,", StringComparison.Ordinal))
            {
                if (req.PendingUploads.IsEmpty && req.PendingDownloads.IsEmpty) return;
                HandleAckInstruction(req, raw);
            }
        }

        // client -> guacd direction (ProxyWebSocketToGuacd). A single WS message may
        // carry several concatenated instructions, mirroring ContainsUserActivity's scan.
        internal static void ObserveClientToServerFileTransfer(GuacSessionRequest req, byte[] payload)
        {
            if (req.FileTransferPolicy == Domain.Enums.FileTransferPolicy.Disabled) return;

            var text = Encoding.UTF8.GetString(payload);
            int i = 0;
            while (i < text.Length)
            {
                int dot = text.IndexOf('.', i);
                if (dot < 0) break;
                if (!int.TryParse(text.AsSpan(i, dot - i), out var len) || len < 0) break;

                int opStart = dot + 1;
                if (opStart + len > text.Length) break;
                var opcode = text.AsSpan(opStart, len);

                if (opcode.SequenceEqual("put"))
                {
                    HandlePutInstruction(req, text, i);
                }
                else if (opcode.SequenceEqual("blob") && !req.PendingUploads.IsEmpty)
                {
                    HandleUploadBlobInstruction(req, text, i);
                }

                int semicolon = text.IndexOf(';', opStart + len);
                if (semicolon < 0) break;
                i = semicolon + 1;
            }
        }

        // `body,object,stream,mimetype,name;` — guacd allocated a stream in response to
        // a client `get`. Tracked as a pending download unless it's a directory listing
        // (root "/" or subdirectory browsing — not an actual file transfer).
        private static void HandleFileTransferBody(GuacSessionRequest req, string raw)
        {
            var parsed = ParseInstruction(raw);
            if (parsed == null || parsed.Count < 5) return;
            if (!int.TryParse(parsed[2], out var streamIndex)) return;
            if (string.Equals(parsed[3], DirectoryListingMimetype, StringComparison.Ordinal)) return;
            req.PendingDownloads[streamIndex] = new PendingFileTransfer { Name = parsed[4] };
        }

        // `put,object,stream,mimetype,name;` — client is opening an upload stream. Rare
        // (once per uploaded file), so a full parse of the bounded instruction slice is fine.
        private static void HandlePutInstruction(GuacSessionRequest req, string text, int startPos)
        {
            int semicolon = text.IndexOf(';', startPos);
            if (semicolon < 0) return;
            var parsed = ParseInstruction(text[startPos..(semicolon + 1)]);
            if (parsed == null || parsed.Count < 5) return;
            if (!int.TryParse(parsed[2], out var streamIndex)) return;
            req.PendingUploads[streamIndex] = new PendingFileTransfer { Name = parsed[4] };
        }

        private static void HandleUploadBlobInstruction(GuacSessionRequest req, string text, int startPos)
        {
            if (!TryReadStreamAndDataField(text, startPos, out var streamIndex, out var dataStart, out var dataLen)) return;
            if (req.PendingUploads.TryGetValue(streamIndex, out var transfer))
            {
                transfer.Bytes += DecodedBase64Length(text.AsSpan(dataStart, dataLen));
            }
        }

        // `ack,stream,message,status;` — status "0" (or omitted) means success. Finalizes
        // an upload (client was the blob sender, server acks completion/failure) or, if a
        // non-zero status arrives for a stream we're still downloading, aborts it — the
        // protocol allows a non-zero ack to implicitly terminate any open stream.
        private void HandleAckInstruction(GuacSessionRequest req, string raw)
        {
            var parsed = ParseInstruction(raw);
            if (parsed == null || parsed.Count < 2) return;
            if (!int.TryParse(parsed[1], out var streamIndex)) return;
            var status = parsed.Count >= 4 ? parsed[3] : "0";
            var success = status == "0";

            if (req.PendingUploads.TryRemove(streamIndex, out var uploadTransfer))
            {
                QueueFileTransferAudit(req, "upload", uploadTransfer.Name, uploadTransfer.Bytes,
                    success ? "success" : "failure", success ? null : status);
                return;
            }

            if (!success && req.PendingDownloads.TryRemove(streamIndex, out var downloadTransfer))
            {
                QueueFileTransferAudit(req, "download", downloadTransfer.Name, downloadTransfer.Bytes, "failure", status);
            }
        }

        // Fire-and-forget: writes the audit log + enqueues matching webhooks on a fresh
        // scope. Called from the hot relay loop, so it must never block it on a DB
        // round-trip — mirrors the recording-upload fire-and-forget trigger.
        private void QueueFileTransferAudit(
            GuacSessionRequest req, string direction, string fileName, long sizeBytes,
            string outcome = "success", string? failureCode = null)
        {
            _ = Task.Run(() => WriteFileTransferAuditAsync(req, direction, fileName, sizeBytes, outcome, failureCode));
        }

        internal async Task WriteFileTransferAuditAsync(
            GuacSessionRequest req, string direction, string fileName, long sizeBytes, string outcome, string? failureCode)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var webhookEnqueuer = scope.ServiceProvider.GetRequiredService<IWebhookEnqueuer>();

                var entry = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = req.User.Id,
                    Action = "session.file_transferred",
                    ResourceType = ResourceTypeConnection,
                    ResourceId = req.Connection.Id.ToString(),
                    Details = JsonSerializer.Serialize(new
                    {
                        sessionId = req.SessionId,
                        connectionId = req.Connection.Id,
                        direction,
                        fileName,
                        sizeBytes,
                        failureCode,
                    }),
                    IpAddress = req.IpAddress,
                    Outcome = outcome,
                };
                db.AuditLogs.Add(entry);
                await webhookEnqueuer.EnqueueAsync(entry);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write session.file_transferred audit log for session {SessionId}", req.SessionId);
            }
        }

        // Reads one `LEN.VALUE` field starting at `pos` (an index into raw), advances
        // `pos` past the trailing separator (',' or ';'), and returns the value's
        // start/length. The building block for the lean extractors below — avoids the
        // List<string> allocation ParseInstruction incurs, which matters here because
        // blob/end fire constantly for ordinary display updates too.
        private static bool TryReadField(string raw, ref int pos, out int start, out int len)
        {
            start = 0; len = 0;
            int dot = raw.IndexOf('.', pos);
            if (dot < 0) return false;
            if (!int.TryParse(raw.AsSpan(pos, dot - pos), out len) || len < 0) return false;
            start = dot + 1;
            if (start + len >= raw.Length) return false;
            pos = start + len + 1;
            return true;
        }

        // `blob,stream,data;` (or `put,object,stream,...` for the first two fields) —
        // extracts the stream index and the data field's span without allocating a
        // copy of the (potentially large) base64 payload.
        private static bool TryReadStreamAndDataField(string raw, int startPos, out int streamIndex, out int dataStart, out int dataLen)
        {
            streamIndex = -1; dataStart = 0; dataLen = 0;
            int pos = startPos;
            if (!TryReadField(raw, ref pos, out _, out _)) return false; // opcode
            if (!TryReadField(raw, ref pos, out var idxStart, out var idxLen)) return false;
            if (!int.TryParse(raw.AsSpan(idxStart, idxLen), out streamIndex)) return false;
            return TryReadField(raw, ref pos, out dataStart, out dataLen);
        }

        // `end,stream;` — extracts just the stream index.
        private static int ReadSingleStreamIndex(string raw, int startPos)
        {
            int pos = startPos;
            if (!TryReadField(raw, ref pos, out _, out _)) return -1; // opcode
            if (!TryReadField(raw, ref pos, out var idxStart, out var idxLen)) return -1;
            return int.TryParse(raw.AsSpan(idxStart, idxLen), out var idx) ? idx : -1;
        }

        // Guacamole blobs are standard-padded base64, each chunk independently decodable —
        // so the decoded length can be computed from the padding without decoding at all.
        private static long DecodedBase64Length(ReadOnlySpan<char> base64)
        {
            if (base64.IsEmpty) return 0;
            var padding = 0;
            if (base64[^1] == '=') padding++;
            if (base64.Length > 1 && base64[^2] == '=') padding++;
            return base64.Length / 4 * 3 - padding;
        }
    }
}
