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

namespace SmoothOperator.Application.Features.UserGroups.Commands
{
    public sealed record SetUserGroupMembersCommand(Guid GroupId, SetUserGroupMembersRequest Dto, ClaimsPrincipal User)
        : IRequest<bool>;

    public sealed class SetUserGroupMembersCommandHandler : IRequestHandler<SetUserGroupMembersCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public SetUserGroupMembersCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(SetUserGroupMembersCommand request, CancellationToken cancellationToken)
        {
            if (request.Dto.UserIds == null)
                throw new BadRequestException("UserIds is required.");

            var group = await _context.UserGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);
            if (group == null)
                throw new NotFoundException("User group not found.");

            var requestedIds = request.Dto.UserIds.Distinct().ToList();
            var userInfos = await _context.Users
                .Where(u => requestedIds.Contains(u.Id))
                .Select(u => new { u.Id, u.IsActive })
                .ToListAsync(cancellationToken);

            if (userInfos.Count != requestedIds.Count)
            {
                var foundIds = userInfos.Select(u => u.Id).ToHashSet();
                var missing = requestedIds.Where(uid => !foundIds.Contains(uid)).ToList();
                throw new BadRequestException($"One or more users do not exist. Missing: {string.Join(", ", missing)}");
            }

            var inactiveIds = userInfos.Where(u => !u.IsActive).Select(u => u.Id).ToList();
            var previousMemberIds = group.Members.Select(m => m.Id).ToList();

            var currentMemberIds = previousMemberIds.ToHashSet();
            var newRequestedIds = requestedIds.ToHashSet();

            if (group.Members is List<User> list)
            {
                list.RemoveAll(m => !newRequestedIds.Contains(m.Id));
            }
            else
            {
                var toRemove = group.Members.Where(m => !newRequestedIds.Contains(m.Id)).ToList();
                foreach (var user in toRemove)
                    group.Members.Remove(user);
            }

            var toAddIds = newRequestedIds.Where(uid => !currentMemberIds.Contains(uid)).ToList();
            foreach (var newId in toAddIds)
            {
                var dummyUser = new User { Id = newId };
                _context.Users.Attach(dummyUser);
                group.Members.Add(dummyUser);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("group.members.updated", "UserGroup", request.GroupId.ToString(),
                new { previousMemberIds, newMemberIds = requestedIds, inactiveAssigned = inactiveIds });

            return true;
        }
    }
}
