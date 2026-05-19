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

namespace SmoothOperator.Application.Features.ConnectionGroups.Queries
{
    public sealed record GetEffectiveUsersQuery(Guid VaultId, ClaimsPrincipal User) : IRequest<VaultEffectiveUsersDto>;

    public sealed class GetEffectiveUsersQueryHandler : IRequestHandler<GetEffectiveUsersQuery, VaultEffectiveUsersDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public GetEffectiveUsersQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<VaultEffectiveUsersDto> Handle(GetEffectiveUsersQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            if (!profile.IsOwnerOrAdmin && !profile.VaultIds.Contains(request.VaultId))
                throw new ForbiddenException("Access denied.");

            var vault = await _context.ConnectionGroups
                .AsNoTracking()
                .Include(v => v.Users).ThenInclude(u => u.Roles)
                .Include(v => v.Groups).ThenInclude(g => g.Members).ThenInclude(u => u.Roles)
                .FirstOrDefaultAsync(v => v.Id == request.VaultId, cancellationToken);

            if (vault == null)
                throw new NotFoundException("Vault not found.");

            var directIds = vault.Users.Select(u => u.Id).ToHashSet();

            var groupedByUser = vault.Groups
                .SelectMany(g => g.Members.Select(u => new { UserId = u.Id, User = u, GroupId = g.Id }))
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var allUserIds = directIds.Union(groupedByUser.Keys).ToList();
            var users = new List<EffectiveUserSourceDto>();

            // Optimize: O(1) lookup dictionary instead of O(N) linear scan inside the loop
            var directUsersDict = vault.Users.ToDictionary(u => u.Id);

            foreach (var uid in allUserIds)
            {
                directUsersDict.TryGetValue(uid, out var directUser);
                groupedByUser.TryGetValue(uid, out var viaGroups);

                var name = directUser?.Name ?? viaGroups![0].User.Name;
                var email = directUser?.Email ?? viaGroups![0].User.Email;
                var isActive = directUser?.IsActive ?? viaGroups![0].User.IsActive;
                var roles = (directUser?.Roles ?? viaGroups![0].User.Roles)
                    .Select(r => r.Name).ToList();

                users.Add(new EffectiveUserSourceDto
                {
                    Id = uid,
                    Name = name,
                    Email = email,
                    IsActive = isActive,
                    Roles = roles,
                    DirectAssignment = directIds.Contains(uid),
                    ViaGroupIds = viaGroups?.Select(v => v.GroupId).Distinct().ToList() ?? []
                });
            }

            return new VaultEffectiveUsersDto
            {
                VaultId = vault.Id,
                VaultName = vault.Name,
                Users = users.OrderBy(u => u.Name).ToList()
            };
        }
    }
}
