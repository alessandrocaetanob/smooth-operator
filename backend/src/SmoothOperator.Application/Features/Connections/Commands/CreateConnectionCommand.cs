using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Connections.Commands
{
    public sealed record CreateConnectionCommand(CreateConnectionDto Dto, ClaimsPrincipal User)
        : IRequest<ConnectionDto>;

    public sealed class CreateConnectionCommandHandler : IRequestHandler<CreateConnectionCommand, ConnectionDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;
        private readonly IAuditService _audit;

        public CreateConnectionCommandHandler(IAppDbContext context, IAccessControlService access, IAuditService audit)
        {
            _context = context;
            _access = access;
            _audit = audit;
        }

        public async Task<ConnectionDto> Handle(CreateConnectionCommand request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            if (!_access.CanManageConnectionsInVault(profile, request.Dto.ConnectionGroupId))
                throw new ForbiddenException("You do not have permission to manage connections in this vault.");

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                Name = request.Dto.Name,
                Protocol = request.Dto.Protocol,
                HostId = request.Dto.HostId,
                CredentialId = request.Dto.CredentialId,
                ConnectionGroupId = request.Dto.ConnectionGroupId,
                Settings = request.Dto.Settings,
                Tags = request.Dto.Tags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(t => new ConnectionTag { Tag = t.Trim().ToLowerInvariant() })
                    .ToList()
            };

            _context.Connections.Add(connection);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("connection.created", "Connection", connection.Id.ToString(),
                new { connection.Name, connection.Protocol, connection.HostId, connection.ConnectionGroupId });

            return new ConnectionDto
            {
                Id = connection.Id,
                Name = connection.Name,
                Protocol = connection.Protocol,
                HostId = connection.HostId,
                CredentialId = connection.CredentialId,
                ConnectionGroupId = connection.ConnectionGroupId,
                Settings = connection.Settings,
                Tags = connection.Tags.Select(t => t.Tag).ToList()
            };
        }
    }
}
