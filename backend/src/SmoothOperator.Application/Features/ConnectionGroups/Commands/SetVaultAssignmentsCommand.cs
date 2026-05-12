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

            // ⚡ Bolt Optimization: Use HashSets and delta updates instead of .Clear() and .Add().
            // This prevents EF Core from generating DELETE/INSERT statements for all existing relationships,
            // reducing database churn, index fragmentation, and massive transaction overhead.
            var currentUsersIds = vault.Users.Select(u => u.Id).ToHashSet();
            var newUsersIds = users.Select(u => u.Id).ToHashSet();

            if (vault.Users is List<User> listUsers)
            {
                // List.RemoveAll is an O(N) linear scan optimization over repeated List.Remove O(N*M).
                listUsers.RemoveAll(u => !newUsersIds.Contains(u.Id));
            }
            else
            {
                var toRemoveUsers = vault.Users.Where(u => !newUsersIds.Contains(u.Id)).ToList();
                foreach (var user in toRemoveUsers)
                    vault.Users.Remove(user);
            }

            var toAddUsersIds = newUsersIds.Where(uid => !currentUsersIds.Contains(uid)).ToList();
            foreach (var newId in toAddUsersIds)
            {
                var user = users.First(u => u.Id == newId);
                vault.Users.Add(user);
            }

            var currentGroupsIds = vault.Groups.Select(g => g.Id).ToHashSet();
            var newGroupsIds = groups.Select(g => g.Id).ToHashSet();

            if (vault.Groups is List<UserGroup> listGroups)
            {
                listGroups.RemoveAll(g => !newGroupsIds.Contains(g.Id));
            }
            else
            {
                var toRemoveGroups = vault.Groups.Where(g => !newGroupsIds.Contains(g.Id)).ToList();
                foreach (var group in toRemoveGroups)
                    vault.Groups.Remove(group);
            }

            var toAddGroupsIds = newGroupsIds.Where(gid => !currentGroupsIds.Contains(gid)).ToList();
            foreach (var newId in toAddGroupsIds)
            {
                var group = groups.First(g => g.Id == newId);
                vault.Groups.Add(group);
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
