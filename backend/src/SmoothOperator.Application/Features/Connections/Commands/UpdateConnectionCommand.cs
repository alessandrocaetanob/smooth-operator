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
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Connections.Commands
{
    public sealed record UpdateConnectionCommand(Guid Id, CreateConnectionDto Dto, ClaimsPrincipal User)
        : IRequest<bool>;

    public sealed class UpdateConnectionCommandHandler : IRequestHandler<UpdateConnectionCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;
        private readonly IAuditService _audit;

        public UpdateConnectionCommandHandler(IAppDbContext context, IAccessControlService access, IAuditService audit)
        {
            _context = context;
            _access = access;
            _audit = audit;
        }

        public async Task<bool> Handle(UpdateConnectionCommand request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var connection = await _context.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (connection == null)
                throw new NotFoundException("Connection not found.");

            var canManageCurrent = _access.CanManageConnectionsInVault(profile, connection.ConnectionGroupId);
            var canManageTarget = _access.CanManageConnectionsInVault(profile, request.Dto.ConnectionGroupId);
            if (!canManageCurrent || !canManageTarget)
                throw new ForbiddenException("You do not have permission to manage connections in this vault.");

            connection.Name = request.Dto.Name;
            connection.Protocol = request.Dto.Protocol;
            connection.HostId = request.Dto.HostId;
            connection.CredentialId = request.Dto.CredentialId;
            connection.ConnectionGroupId = request.Dto.ConnectionGroupId;
            connection.Settings = request.Dto.Settings;

            _context.ConnectionTags.RemoveRange(connection.Tags);
            connection.Tags = request.Dto.Tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(t => new ConnectionTag { Tag = t.Trim().ToLowerInvariant(), ConnectionId = request.Id })
                .ToList();

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("connection.updated", "Connection", request.Id.ToString(),
                new { connection.Name, connection.Protocol, connection.ConnectionGroupId });

            return true;
        }
    }
}
