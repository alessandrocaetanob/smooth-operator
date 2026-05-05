using System;
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
    public sealed record GetMyLastConnectedQuery(ClaimsPrincipal User) : IRequest<IEnumerable<Guid>>;

    public sealed class GetMyLastConnectedQueryHandler : IRequestHandler<GetMyLastConnectedQuery, IEnumerable<Guid>>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public GetMyLastConnectedQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<IEnumerable<Guid>> Handle(GetMyLastConnectedQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var results = await _context.AuditLogs
                .Where(a => a.UserId == profile.UserId && a.Action == "connection.started")
                .GroupBy(a => a.ResourceId)
                .Select(g => new { ResourceId = g.Key, LastConnectedAt = g.Max(a => a.Timestamp) })
                .OrderByDescending(x => x.LastConnectedAt)
                .ToListAsync(cancellationToken);

            return results
                .Where(r => Guid.TryParse(r.ResourceId, out _))
                .Select(r => Guid.Parse(r.ResourceId));
        }
    }
}
