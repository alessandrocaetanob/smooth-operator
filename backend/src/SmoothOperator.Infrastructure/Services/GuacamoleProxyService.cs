using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Exceptions;
using System;
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
    public class GuacamoleProxyService
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

        private static readonly TimeSpan TicketTtl = TimeSpan.FromSeconds(30);

        public GuacamoleProxyService(
            ILogger<GuacamoleProxyService> logger,
            IOptions<GuacdOptions> guacdOptions,
            IServiceScopeFactory scopeFactory,
            IEncryptionService encryptionService,
            IConnectionMultiplexer redis,
            IAppMetrics metrics,
            ISecretProviderFactory secretProviderFactory)
        {
            _logger = logger;
            _guacdHost = guacdOptions.Value.Host;
            _guacdPort = guacdOptions.Value.Port;
            _scopeFactory = scopeFactory;
            _encryptionService = encryptionService;
            _redis = redis;
            _metrics = metrics;
            _secretProviderFactory = secretProviderFactory;
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

            await RunGuacamoleSessionAsync(new GuacSessionRequest(
                webSocket,
                connection,
                sessionId,
                dbUser,
                ipAddress,
                dbContext,
                db,
                sessionSettingsOverrides));
        }

        private sealed record GuacSessionRequest(
            WebSocket WebSocket,
            Connection Connection,
            string SessionId,
            User User,
            string IpAddress,
            AppDbContext DbContext,
            IDatabase RedisDb,
            IDictionary<string, string>? SettingsOverrides);

        private static Task<User?> LoadAuthorizedUserAsync(AppDbContext dbContext, Guid userId) =>
            dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        private static Task<Connection?> LoadConnectionAsync(AppDbContext dbContext, Guid connectionId) =>
            dbContext.Connections
                .Include(c => c.Host)
                .Include(c => c.Credential).ThenInclude(cr => cr!.SecretProvider)
                .Include(c => c.Users)
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
                ParseSettings(connection.Settings).GetValueOrDefault("port"),
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
                    sessionId = sessionId
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

                await PerformGuacamoleHandshakeAsync(
                    networkStream,
                    reader,
                    req.Connection,
                    req.DbContext,
                    req.User,
                    req.IpAddress,
                    req.SettingsOverrides);

                _metrics.RecordConnectionStarted();
                sessionMetricRecorded = true;
                connectionSuccessful = true;

                await RunBidirectionalProxyAsync(reader, req.WebSocket, networkStream, req.Connection.Id, msg =>
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
                    await LogConnectionEndedAsync(req.DbContext, req.User.Id, req.Connection.Id, req.IpAddress, req.SessionId);
                }

                if (sessionMetricRecorded)
                {
                    _metrics.RecordConnectionEnded();
                }

                await CloseGuacSessionAsync(req.WebSocket, failureReason);
                tcpClient.Close();
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
                CancellationToken.None,
                dbUser.Id,
                ipAddress,
                sessionSettingsOverrides));

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

        private static async Task RunBidirectionalProxyAsync(
            GuacInstructionReader reader,
            WebSocket webSocket,
            NetworkStream networkStream,
            Guid connectionId,
            Action<string> onGuacError)
        {
            // Bidirectional proxy with proper instruction framing on the guacd→ws side.
            _ = connectionId; // reserved for future telemetry tagging
            var receiveTask = ProxyGuacdToWebSocket(reader, webSocket, onGuacError);
            var sendTask = ProxyWebSocketToGuacd(webSocket, networkStream);
            await Task.WhenAny(receiveTask, sendTask);
        }

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
            AppDbContext dbContext, Guid userId, Guid connectionId, string ipAddress, string sessionId)
        {
            var endTime = DateTime.UtcNow;
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "connection.ended",
                ResourceType = ResourceTypeConnection,
                ResourceId = connectionId.ToString(),
                Details = $"{{\"sessionId\":\"{sessionId}\",\"endTime\":\"{endTime:O}\"}}",
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
            CancellationToken CancellationToken,
            Guid UserId,
            string IpAddress,
            IDictionary<string, string>? SettingsOverrides);

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
            Dictionary<string, string> overrides = ParseSettings(req.Connection.Settings);
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

        private static Dictionary<string, string> ParseSettings(string? settingsJson)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(settingsJson)) return dict;
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => string.Empty,
                        _ => prop.Value.ToString()
                    };
                }
            }
            catch
            {
                // Silently ignore malformed Settings — it's user-controlled JSON.
            }
            return dict;
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
            await stream.WriteAsync(data, 0, data.Length);
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
                        return new List<string>();
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

        private static async Task ProxyGuacdToWebSocket(GuacInstructionReader reader, WebSocket webSocket, Action<string> onGuacError)
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

        private static async Task ProxyWebSocketToGuacd(WebSocket webSocket, NetworkStream guacdStream)
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
                    await ms.WriteAsync(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var payload = ms.ToArray();
                if (payload.Length == 0) continue;
                await guacdStream.WriteAsync(payload, 0, payload.Length);
                await guacdStream.FlushAsync();
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
    }
}
