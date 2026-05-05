using System;
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

namespace SmoothOperator.Application.Features.ConnectionGroups.Commands
{
    public sealed record SetVaultAssignmentsCommand(Guid VaultId, VaultAssignmentsDto Dto, ClaimsPrincipal User)
        : IRequest<VaultAssignmentsDto>;

    public sealed class SetVaultAssignmentsCommandHandler : IRequestHandler<SetVaultAssignmentsCommand, VaultAssignmentsDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public SetVaultAssignmentsCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<VaultAssignmentsDto> Handle(SetVaultAssignmentsCommand request, CancellationToken cancellationToken)
        {
            var vault = await _context.ConnectionGroups
                .Include(v => v.Users)
                .Include(v => v.Groups)
                .FirstOrDefaultAsync(v => v.Id == request.VaultId, cancellationToken);

            if (vault == null)
                throw new NotFoundException("Vault not found.");

            var requestedUserIds = (request.Dto.UserIds ?? new List<Guid>()).Distinct().ToList();
            var requestedGroupIds = (request.Dto.GroupIds ?? new List<Guid>()).Distinct().ToList();

            var users = await _context.Users
                .Where(u => requestedUserIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            var groups = await _context.UserGroups
                .Where(g => requestedGroupIds.Contains(g.Id))
                .ToListAsync(cancellationToken);

            var missingUserIds = requestedUserIds.Except(users.Select(u => u.Id)).ToList();
            var missingGroupIds = requestedGroupIds.Except(groups.Select(g => g.Id)).ToList();

            if (missingUserIds.Count > 0 || missingGroupIds.Count > 0)
                throw new BadRequestException("One or more assignment IDs are invalid.");

            vault.Users.Clear();
            foreach (var u in users) vault.Users.Add(u);

            vault.Groups.Clear();
            foreach (var g in groups) vault.Groups.Add(g);

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("vault.assignments.updated", "ConnectionGroup", request.VaultId.ToString(),
                new { UserCount = users.Count, GroupCount = groups.Count });

            return new VaultAssignmentsDto
            {
                UserIds = users.Select(u => u.Id).ToList(),
                GroupIds = groups.Select(g => g.Id).ToList()
            };
        }
    }
}
