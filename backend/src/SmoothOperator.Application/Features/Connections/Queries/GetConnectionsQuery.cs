using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Connections.Queries
{
    public sealed record GetConnectionsQuery(ClaimsPrincipal User) : IRequest<IEnumerable<ConnectionDto>>;

    public sealed class GetConnectionsQueryHandler : IRequestHandler<GetConnectionsQuery, IEnumerable<ConnectionDto>>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public GetConnectionsQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<IEnumerable<ConnectionDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var connections = await _access.ApplyConnectionScope(
                    _context.Connections
                        .Include(c => c.Host)
                        .Include(c => c.ConnectionGroup)
                        .Include(c => c.Tags),
                    profile)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return connections.Select(Project);
        }

        internal static ConnectionDto Project(Domain.Models.Connection c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Protocol = c.Protocol,
            HostId = c.HostId,
            CredentialId = c.CredentialId,
            ConnectionGroupId = c.ConnectionGroupId,
            Settings = c.Settings,
            Tags = c.Tags.Select(t => t.Tag).ToList(),
            Host = c.Host == null ? null : new HostDto
            {
                Id = c.Host.Id,
                Name = c.Host.Name,
                Address = c.Host.Address
            },
            ConnectionGroup = c.ConnectionGroup == null ? null : new ConnectionGroupDto
            {
                Id = c.ConnectionGroup.Id,
                Name = c.ConnectionGroup.Name
            }
        };
    }
}
