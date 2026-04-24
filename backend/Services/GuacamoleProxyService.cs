using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Backend.Services
{
    public class GuacamoleProxyService
    {
        private readonly ILogger<GuacamoleProxyService> _logger;
        private readonly string _guacdHost;
        private readonly int _guacdPort;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEncryptionService _encryptionService;
        private readonly IConnectionMultiplexer _redis;

        public GuacamoleProxyService(
            ILogger<GuacamoleProxyService> logger,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            IEncryptionService encryptionService)
        {
            _logger = logger;
            _guacdHost = configuration["Guacd:Host"] ?? "guacd";
            _guacdPort = int.Parse(configuration["Guacd:Port"] ?? "4822");
            _scopeFactory = scopeFactory;
            _encryptionService = encryptionService;

            var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            _redis = ConnectionMultiplexer.Connect(redisConnectionString);
        }

        public async Task HandleWebSocketAsync(WebSocket webSocket, Guid connectionId, ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var sessionId = Guid.NewGuid().ToString();

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var connection = await dbContext.Connections
                .Include(c => c.Host)
                .Include(c => c.Credential)
                .FirstOrDefaultAsync(c => c.Id == connectionId);

            if (connection == null || connection.Host == null)
            {
                _logger.LogWarning($"Connection {connectionId} not found or has no host.");
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Connection not found", CancellationToken.None);
                return;
            }

            // Register session in Redis
            var db = _redis.GetDatabase();
            await db.HashSetAsync($"session:{sessionId}", new HashEntry[]
            {
                new HashEntry("UserId", userId),
                new HashEntry("ConnectionId", connectionId.ToString()),
                new HashEntry("StartTime", DateTime.UtcNow.ToString("O"))
            });
            // Expire just in case it doesn't get cleaned up
            await db.KeyExpireAsync($"session:{sessionId}", TimeSpan.FromHours(24));

            using var tcpClient = new TcpClient();
            try
            {
                await tcpClient.ConnectAsync(_guacdHost, _guacdPort);
                using var networkStream = tcpClient.GetStream();

                // Build initial Guacamole Handshake
                string protocol = connection.Protocol;
                string selectCmd = BuildGuacInstruction("select", protocol);
                await SendGuacMessage(networkStream, selectCmd);

                // Wait for args response from guacd
                string argsResponse = await ReadGuacInstruction(networkStream);

                // Construct standard connection parameters for RDP/SSH/VNC
                string sizeCmd = BuildGuacInstruction("size", "1024", "768", "96");
                string audioCmd = BuildGuacInstruction("audio", "audio/L16");
                string videoCmd = BuildGuacInstruction("video");
                string imageCmd = BuildGuacInstruction("image", "image/png", "image/jpeg");

                await SendGuacMessage(networkStream, sizeCmd);
                await SendGuacMessage(networkStream, audioCmd);
                await SendGuacMessage(networkStream, videoCmd);
                await SendGuacMessage(networkStream, imageCmd);

                // Build Connect Command
                string hostname = connection.Host.Address;
                string port = protocol == "rdp" ? "3389" : (protocol == "ssh" ? "22" : "5900");
                string username = connection.Credential?.Username ?? "";

                string password = "";
                if (connection.Credential != null && !string.IsNullOrEmpty(connection.Credential.EncryptedSecret))
                {
                    password = _encryptionService.Decrypt(connection.Credential.EncryptedSecret);
                }

                // Highly simplified connect command construction for PoC
                // In reality, you must match the exact parameter order specified in the 'args' response.
                string connectCmd = BuildGuacInstruction("connect",
                    "",
                    hostname,
                    port,
                    username,
                    password,
                    "true" // ignore-cert
                );

                await SendGuacMessage(networkStream, connectCmd);

                // Start bidirectional proxy
                var receiveTask = ProxyGuacdToWebSocket(networkStream, webSocket);
                var sendTask = ProxyWebSocketToGuacd(webSocket, networkStream);

                await Task.WhenAny(receiveTask, sendTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Guacamole session.");
            }
            finally
            {
                // Cleanup Redis
                await db.KeyDeleteAsync($"session:{sessionId}");

                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
                tcpClient.Close();
            }
        }

        private string BuildGuacInstruction(params string[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string arg = args[i] ?? "";
                sb.Append($"{arg.Length}.{arg}");
            }
            sb.Append(';');
            return sb.ToString();
        }

        private async Task SendGuacMessage(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }

        private async Task<string> ReadGuacInstruction(NetworkStream stream)
        {
            var buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        private async Task ProxyGuacdToWebSocket(NetworkStream guacdStream, WebSocket webSocket)
        {
            var buffer = new byte[8192];
            while (webSocket.State == WebSocketState.Open)
            {
                int bytesRead = await guacdStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, bytesRead),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }

        private async Task ProxyWebSocketToGuacd(WebSocket webSocket, NetworkStream guacdStream)
        {
            var buffer = new byte[8192];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;

                await guacdStream.WriteAsync(buffer, 0, result.Count);
            }
        }
    }
}
