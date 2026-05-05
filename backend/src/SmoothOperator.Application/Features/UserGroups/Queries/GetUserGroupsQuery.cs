using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.UserGroups.Queries
{
    public sealed record GetUserGroupsQuery(ClaimsPrincipal User) : IRequest<IEnumerable<UserGroupDto>>;

    public sealed class GetUserGroupsQueryHandler : IRequestHandler<GetUserGroupsQuery, IEnumerable<UserGroupDto>>
    {
        private readonly IAppDbContext _context;

        public GetUserGroupsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserGroupDto>> Handle(GetUserGroupsQuery request, CancellationToken cancellationToken)
        {
            return await _context.UserGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .Include(g => g.Owner)
                .OrderBy(g => g.Name)
                .Select(g => new UserGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    OwnerUserId = g.OwnerUserId,
                    OwnerName = g.Owner != null ? g.Owner.Name : null,
                    CreatedAt = g.CreatedAt,
                    MemberCount = g.Members.Count,
                    VaultCount = g.Vaults.Count,
                    Members = g.Members
                        .OrderBy(m => m.Name)
                        .Select(m => new UserGroupMemberDto
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Email = m.Email,
                            IsActive = m.IsActive
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);
        }
    }
}
