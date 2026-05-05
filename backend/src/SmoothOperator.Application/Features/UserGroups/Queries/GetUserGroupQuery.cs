using System;
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
    public sealed record GetUserGroupQuery(Guid Id, ClaimsPrincipal User) : IRequest<UserGroupDto>;

    public sealed class GetUserGroupQueryHandler : IRequestHandler<GetUserGroupQuery, UserGroupDto>
    {
        private readonly IAppDbContext _context;

        public GetUserGroupQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<UserGroupDto> Handle(GetUserGroupQuery request, CancellationToken cancellationToken)
        {
            var dto = await _context.UserGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .Include(g => g.Owner)
                .Where(g => g.Id == request.Id)
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
                .FirstOrDefaultAsync(cancellationToken);

            if (dto == null)
                throw new NotFoundException("User group not found.");

            return dto;
        }
    }
}
