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

            var existingConnections = await LoadExistingConnectionsAsync(ids, cancellationToken);
            var existingSet = existingConnections.Select(c => c.Id).ToHashSet();

            var accessibleSet = await LoadAccessibleIdSetAsync(ids, profile, cancellationToken);
            var accessibleById = existingConnections
                .Where(c => accessibleSet.Contains(c.Id))
                .ToDictionary(c => c.Id);

            var hostLookup = await LoadHostLookupAsync(accessibleById.Values, cancellationToken);

            var results = new ConcurrentDictionary<Guid, string>();
            ClassifyMissingOrInaccessible(ids, existingSet, accessibleSet, accessibleById, hostLookup, results);

            await ProbeReachableConnectionsAsync(accessibleById.Values, hostLookup, results, cancellationToken);

            return new Dictionary<Guid, string>(results);
        }

        private async Task<List<ConnectionProbeInfo>> LoadExistingConnectionsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
            await _context.Connections
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new ConnectionProbeInfo(c.Id, c.Protocol, c.Settings, c.HostId))
                .ToListAsync(cancellationToken);

        private async Task<HashSet<Guid>> LoadAccessibleIdSetAsync(IReadOnlyList<Guid> ids, AccessProfile profile, CancellationToken cancellationToken)
        {
            var accessibleIds = await _access.ApplyConnectionScope(_context.Connections.AsNoTracking(), profile)
                .Where(c => ids.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            return accessibleIds.ToHashSet();
        }

        private async Task<Dictionary<Guid, string>> LoadHostLookupAsync(IEnumerable<ConnectionProbeInfo> accessible, CancellationToken cancellationToken)
        {
            var hostIds = accessible.Select(c => c.HostId).Distinct().ToList();
            return await _context.Hosts
                .AsNoTracking()
                .Where(h => hostIds.Contains(h.Id))
                .Select(h => new { h.Id, h.Address })
                .ToDictionaryAsync(h => h.Id, h => h.Address, cancellationToken);
        }

        private static void ClassifyMissingOrInaccessible(
            IReadOnlyList<Guid> ids,
            HashSet<Guid> existingSet,
            HashSet<Guid> accessibleSet,
            Dictionary<Guid, ConnectionProbeInfo> accessibleById,
            Dictionary<Guid, string> hostLookup,
            ConcurrentDictionary<Guid, string> results)
        {
            foreach (var id in ids)
            {
                var status = ClassifyId(id, existingSet, accessibleSet, accessibleById, hostLookup);
                if (status != null)
                    results[id] = status;
            }
        }

        private static string? ClassifyId(
            Guid id,
            HashSet<Guid> existingSet,
            HashSet<Guid> accessibleSet,
            Dictionary<Guid, ConnectionProbeInfo> accessibleById,
            Dictionary<Guid, string> hostLookup)
        {
            if (!existingSet.Contains(id))
                return "not_found";
            if (!accessibleSet.Contains(id))
                return "forbidden";
            if (!hostLookup.ContainsKey(accessibleById[id].HostId))
                return "no_host";
            return null;
        }

        private static async Task ProbeReachableConnectionsAsync(
            IEnumerable<ConnectionProbeInfo> accessible,
            Dictionary<Guid, string> hostLookup,
            ConcurrentDictionary<Guid, string> results,
            CancellationToken cancellationToken)
        {
            var probeCandidates = accessible.Where(c => hostLookup.ContainsKey(c.HostId)).ToList();
            using var semaphore = new SemaphoreSlim(BulkProbeMaxConcurrency, BulkProbeMaxConcurrency);

            var tasks = probeCandidates
                .Select(conn => ProbeOneAsync(conn, hostLookup[conn.HostId], semaphore, results, cancellationToken))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private static Task ProbeOneAsync(
            ConnectionProbeInfo conn,
            string hostAddress,
            SemaphoreSlim semaphore,
            ConcurrentDictionary<Guid, string> results,
            CancellationToken cancellationToken) =>
            Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var port = ResolvePort(conn);
                    using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var reachable = await ProbeConnectionQueryHandler.TcpProbeAsync(hostAddress, port, probeCts.Token);
                    results[conn.Id] = reachable ? "up" : "down";
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

        private static int ResolvePort(ConnectionProbeInfo conn)
        {
            var settings = ProbeConnectionQueryHandler.ParseConnectionSettings(conn.Settings);
            var defaultPort = GetDefaultPort(conn.Protocol);
            return int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;
        }

        private static int GetDefaultPort(string? protocol) =>
            (protocol ?? "rdp").ToLowerInvariant() switch
            {
                "ssh" => 22,
                "vnc" => 5900,
                "telnet" => 23,
                _ => 3389
            };

        private sealed record ConnectionProbeInfo(Guid Id, string? Protocol, string? Settings, Guid HostId);
    }
}
