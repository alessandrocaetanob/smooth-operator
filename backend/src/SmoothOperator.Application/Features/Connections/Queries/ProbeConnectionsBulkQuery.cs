using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Connections.Queries
{
    public sealed record ProbeConnectionsBulkQuery(ClaimsPrincipal User) : IRequest<Dictionary<Guid, string>>;

    public sealed class ProbeConnectionsBulkQueryHandler : IRequestHandler<ProbeConnectionsBulkQuery, Dictionary<Guid, string>>
    {
        private const int BulkProbeMaxConcurrency = 10;
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public ProbeConnectionsBulkQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<Dictionary<Guid, string>> Handle(ProbeConnectionsBulkQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var connections = await _access.ApplyConnectionScope(_context.Connections, profile)
                .Select(c => new { c.Id, c.Protocol, c.Settings, c.HostId })
                .ToListAsync(cancellationToken);

            if (connections.Count == 0)
                return new Dictionary<Guid, string>();

            var hostIds = connections.Select(c => c.HostId).Distinct().ToList();
            var hostLookup = await _context.Hosts
                .Where(h => hostIds.Contains(h.Id))
                .Select(h => new { h.Id, h.Address })
                .ToDictionaryAsync(h => h.Id, cancellationToken);

            var results = new ConcurrentDictionary<Guid, string>();

            foreach (var conn in connections.Where(c => !hostLookup.ContainsKey(c.HostId)))
                results[conn.Id] = "no_host";

            var probeCandidates = connections.Where(c => hostLookup.ContainsKey(c.HostId)).ToList();
            using var semaphore = new SemaphoreSlim(BulkProbeMaxConcurrency, BulkProbeMaxConcurrency);

            var tasks = probeCandidates.Select(conn => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var defaultPort = (conn.Protocol ?? "rdp").ToLowerInvariant() switch
                    {
                        "ssh" => 22,
                        "vnc" => 5900,
                        "telnet" => 23,
                        _ => 3389
                    };
                    var settings = ProbeConnectionQueryHandler.ParseConnectionSettings(conn.Settings);
                    var port = int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;

                    using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var reachable = await ProbeConnectionQueryHandler.TcpProbeAsync(
                        hostLookup[conn.HostId].Address, port, probeCts.Token);
                    results[conn.Id] = reachable ? "up" : "down";
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken)).ToList();

            await Task.WhenAll(tasks);
            return new Dictionary<Guid, string>(results);
        }
    }
}
