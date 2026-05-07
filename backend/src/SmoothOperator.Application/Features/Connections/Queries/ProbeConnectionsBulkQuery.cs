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
    public sealed record ProbeConnectionsBulkQuery(IReadOnlyList<Guid> RequestedIds, ClaimsPrincipal User) : IRequest<Dictionary<Guid, string>>;

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

            var ids = request.RequestedIds;
            if (ids.Count == 0)
                return new Dictionary<Guid, string>();

            // 1. Find which requested IDs exist in the database.
            var existingConnections = await _context.Connections
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Protocol, c.Settings, c.HostId, c.ConnectionGroupId })
                .ToListAsync(cancellationToken);

            var existingSet = existingConnections.Select(c => c.Id).ToHashSet();

            // 2. Determine which existing connections the user can access.
            var accessibleIds = await _access.ApplyConnectionScope(_context.Connections.AsNoTracking(), profile)
                .Where(c => ids.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var accessibleSet = accessibleIds.ToHashSet();
            var accessibleById = existingConnections
                .Where(c => accessibleSet.Contains(c.Id))
                .ToDictionary(c => c.Id);

            var hostIds = accessibleById.Values.Select(c => c.HostId).Distinct().ToList();
            var hostLookup = await _context.Hosts
                .AsNoTracking()
                .Where(h => hostIds.Contains(h.Id))
                .Select(h => new { h.Id, h.Address })
                .ToDictionaryAsync(h => h.Id, cancellationToken);

            var results = new ConcurrentDictionary<Guid, string>();

            // 3. Classify each requested ID.
            foreach (var id in ids)
            {
                if (!existingSet.Contains(id))
                    results[id] = "not_found";
                else if (!accessibleSet.Contains(id))
                    results[id] = "forbidden";
                else if (!hostLookup.ContainsKey(accessibleById[id].HostId))
                    results[id] = "no_host";
            }

            // 4. Probe accessible connections that have a host.
            var probeCandidates = accessibleById.Values
                .Where(c => hostLookup.ContainsKey(c.HostId))
                .ToList();

            using var semaphore = new SemaphoreSlim(BulkProbeMaxConcurrency, BulkProbeMaxConcurrency);

            var tasks = probeCandidates.Select(conn => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var defaultPort = GetDefaultPort(conn.Protocol);
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

        private static int GetDefaultPort(string? protocol) =>
            (protocol ?? "rdp").ToLowerInvariant() switch
            {
                "ssh" => 22,
                "vnc" => 5900,
                "telnet" => 23,
                _ => 3389
            };
    }
}
