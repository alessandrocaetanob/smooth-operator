using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Connections.Queries
{
    public sealed record ProbeBatchQuery(IEnumerable<Guid> ConnectionIds, ClaimsPrincipal User)
        : IRequest<Dictionary<Guid, bool>>;

    public sealed class ProbeBatchQueryHandler : IRequestHandler<ProbeBatchQuery, Dictionary<Guid, bool>>
    {
        private const int ProbeBatchConcurrency = 20;
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public ProbeBatchQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<Dictionary<Guid, bool>> Handle(ProbeBatchQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var ids = request.ConnectionIds.ToList();
            var connections = await _access.ApplyConnectionScope(
                    _context.Connections
                        .AsNoTracking()
                        .Include(c => c.Host)
                        .Include(c => c.Users),
                    profile)
                .Where(c => ids.Contains(c.Id))
                .ToListAsync(cancellationToken);

            using var semaphore = new SemaphoreSlim(ProbeBatchConcurrency, ProbeBatchConcurrency);

            var probeTasks = connections.Select(async connection =>
            {
                if (connection.Host == null)
                    return new KeyValuePair<Guid, bool>(connection.Id, false);

                var defaultPort = (connection.Protocol ?? "rdp").ToLowerInvariant() switch
                {
                    "ssh" => 22,
                    "vnc" => 5900,
                    "telnet" => 23,
                    _ => 3389
                };

                var settings = ConnectionSettingsParser.Parse(connection.Settings);
                var port = int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;

                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var reachable = await ProbeConnectionQueryHandler.TcpProbeAsync(connection.Host.Address, port, cts.Token);
                    return new KeyValuePair<Guid, bool>(connection.Id, reachable);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(probeTasks);
            return results.ToDictionary(r => r.Key, r => r.Value);
        }
    }
}
