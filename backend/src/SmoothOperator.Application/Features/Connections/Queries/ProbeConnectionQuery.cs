using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Claims;
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
                .AsNoTracking()
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

            var settings = ConnectionSettingsParser.Parse(connection.Settings);
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
    }
}
