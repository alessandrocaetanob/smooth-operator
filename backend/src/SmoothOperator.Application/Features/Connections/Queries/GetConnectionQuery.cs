using System;
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
    public sealed record GetConnectionQuery(Guid Id, ClaimsPrincipal User) : IRequest<ConnectionDto?>;

    public sealed class GetConnectionQueryHandler : IRequestHandler<GetConnectionQuery, ConnectionDto?>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public GetConnectionQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<ConnectionDto?> Handle(GetConnectionQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var connection = await _access.ApplyConnectionScope(
                    _context.Connections
                        .AsNoTracking()
                        .Include(c => c.Host)
                        .Include(c => c.ConnectionGroup)
                        .Include(c => c.Tags),
                    profile)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (connection == null) return null;

            return GetConnectionsQueryHandler.Project(connection);
        }
    }
}
