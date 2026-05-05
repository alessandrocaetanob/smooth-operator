using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Connections.Queries
{
    public sealed record ProbeConnectionQuery(Guid ConnectionId, ClaimsPrincipal User) : IRequest<string>;

    public sealed class ProbeConnectionQueryHandler : IRequestHandler<ProbeConnectionQuery, string>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public ProbeConnectionQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<string> Handle(ProbeConnectionQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null) return "forbidden";

            var connection = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.Users)
                .FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);

            if (connection == null) return "not_found";
            if (!_access.CanUseConnection(profile, connection)) return "forbidden";
            if (connection.Host == null) return "no_host";

            var defaultPort = (connection.Protocol ?? "rdp").ToLowerInvariant() switch
            {
                "ssh" => 22,
                "vnc" => 5900,
                "telnet" => 23,
                _ => 3389
            };

            var settings = ParseConnectionSettings(connection.Settings);
            var port = int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reachable = await TcpProbeAsync(connection.Host.Address, port, cts.Token);
            return reachable ? "up" : "down";
        }

        internal static async Task<bool> TcpProbeAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port, ct);
                return true;
            }
            catch { return false; }
        }

        internal static Dictionary<string, string> ParseConnectionSettings(string? settingsJson)
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
                        _ => string.Empty
                    };
                }
            }
            catch { /* ignore malformed JSON */ }
            return dict;
        }
    }
}
