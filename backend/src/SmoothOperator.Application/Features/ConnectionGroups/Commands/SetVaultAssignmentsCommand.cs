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
using SmoothOperator.Domain.Models;

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

            var requestedUserIds = (request.Dto.UserIds ?? []).Distinct().ToList();
            var requestedGroupIds = (request.Dto.GroupIds ?? []).Distinct().ToList();

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

            // Performance Optimization: Use delta updates instead of Clear() and Add().
            // This prevents EF Core from generating massive DELETE and INSERT churn
            // for relationships that haven't actually changed.
            var newUserIds = users.Select(u => u.Id).ToHashSet();
            if (vault.Users is List<User> userList)
            {
                userList.RemoveAll(u => !newUserIds.Contains(u.Id));
            }
            else
            {
                var toRemove = vault.Users.Where(u => !newUserIds.Contains(u.Id)).ToList();
                foreach (var u in toRemove) vault.Users.Remove(u);
            }

            var currentUserIds = vault.Users.Select(u => u.Id).ToHashSet();
            foreach (var u in users.Where(u => !currentUserIds.Contains(u.Id)))
            {
                vault.Users.Add(u);
            }

            var newGroupIds = groups.Select(g => g.Id).ToHashSet();
            if (vault.Groups is List<UserGroup> groupList)
            {
                groupList.RemoveAll(g => !newGroupIds.Contains(g.Id));
            }
            else
            {
                var toRemove = vault.Groups.Where(g => !newGroupIds.Contains(g.Id)).ToList();
                foreach (var g in toRemove) vault.Groups.Remove(g);
            }

            var currentGroupIds = vault.Groups.Select(g => g.Id).ToHashSet();
            foreach (var g in groups.Where(g => !currentGroupIds.Contains(g.Id)))
            {
                vault.Groups.Add(g);
            }

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
