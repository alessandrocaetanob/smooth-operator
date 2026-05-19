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
                .Select(UserGroupProjection.ToDto)
                .ToListAsync(cancellationToken);
        }
    }
}
