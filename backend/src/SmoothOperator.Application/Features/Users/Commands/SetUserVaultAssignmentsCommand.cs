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
using SmoothOperator.Application.Features.Users.Queries;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Users.Commands
{
    public sealed record SetUserVaultAssignmentsCommand(Guid UserId, SetUserVaultAssignmentsRequest Dto, ClaimsPrincipal User)
        : IRequest<UserListItemDto>;

    public sealed class SetUserVaultAssignmentsCommandHandler : IRequestHandler<SetUserVaultAssignmentsCommand, UserListItemDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public SetUserVaultAssignmentsCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserListItemDto> Handle(SetUserVaultAssignmentsCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            var requestedIds = (request.Dto.VaultIds ?? [])
                .Where(v => v != Guid.Empty)
                .Distinct()
                .ToList();

            var groups = await _context.ConnectionGroups
                .Where(g => requestedIds.Contains(g.Id))
                .ToListAsync(cancellationToken);

            if (groups.Count != requestedIds.Count)
                throw new BadRequestException("One or more vault IDs are invalid.");

            // Performance Optimization: Use delta updates instead of Clear() and Add().
            // This prevents EF Core from generating massive DELETE and INSERT churn
            // for relationships that haven't actually changed.
            var newGroupIds = groups.Select(g => g.Id).ToHashSet();
            if (user.ConnectionGroups is List<ConnectionGroup> groupList)
            {
                groupList.RemoveAll(g => !newGroupIds.Contains(g.Id));
            }
            else
            {
                var toRemove = user.ConnectionGroups.Where(g => !newGroupIds.Contains(g.Id)).ToList();
                foreach (var g in toRemove) user.ConnectionGroups.Remove(g);
            }

            var currentGroupIds = user.ConnectionGroups.Select(g => g.Id).ToHashSet();
            foreach (var group in groups.Where(g => !currentGroupIds.Contains(g.Id)))
            {
                user.ConnectionGroups.Add(group);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.vaults_updated", "User", user.Id.ToString(),
                new { vaultIds = requestedIds });

            return GetUsersQueryHandler.Project(user);
        }
    }
}
