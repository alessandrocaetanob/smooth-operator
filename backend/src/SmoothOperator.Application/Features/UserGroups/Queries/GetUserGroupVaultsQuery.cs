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

namespace SmoothOperator.Application.Features.UserGroups.Queries
{
    public sealed record GetUserGroupVaultsQuery(Guid GroupId, ClaimsPrincipal User)
        : IRequest<IEnumerable<UserGroupVaultDto>>;

    public sealed class GetUserGroupVaultsQueryHandler : IRequestHandler<GetUserGroupVaultsQuery, IEnumerable<UserGroupVaultDto>>
    {
        private readonly IAppDbContext _context;

        public GetUserGroupVaultsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserGroupVaultDto>> Handle(GetUserGroupVaultsQuery request, CancellationToken cancellationToken)
        {
            var group = await _context.UserGroups
                .AsNoTracking()
                .Include(g => g.Vaults)
                .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

            if (group == null)
                throw new NotFoundException("User group not found.");

            return group.Vaults
                .OrderBy(v => v.Name)
                .Select(v => new UserGroupVaultDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    ParentGroupId = v.ParentGroupId
                })
                .ToList();
        }
    }
}
