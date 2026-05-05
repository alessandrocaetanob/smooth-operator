using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Connections.Commands
{
    public sealed record DeleteConnectionCommand(Guid Id, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class DeleteConnectionCommandHandler : IRequestHandler<DeleteConnectionCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;
        private readonly IAuditService _audit;

        public DeleteConnectionCommandHandler(IAppDbContext context, IAccessControlService access, IAuditService audit)
        {
            _context = context;
            _access = access;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteConnectionCommand request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var connection = await _context.Connections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (connection == null)
                throw new NotFoundException("Connection not found.");

            if (!_access.CanManageConnectionsInVault(profile, connection.ConnectionGroupId))
                throw new ForbiddenException("You do not have permission to manage connections in this vault.");

            _context.Connections.Remove(connection);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("connection.deleted", "Connection", request.Id.ToString(),
                new { connection.Name, connection.ConnectionGroupId });

            return true;
        }
    }
}
