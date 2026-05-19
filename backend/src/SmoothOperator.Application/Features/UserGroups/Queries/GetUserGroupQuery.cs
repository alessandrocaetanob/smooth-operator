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
                .Select(UserGroupProjection.ToDto)
                .FirstOrDefaultAsync(cancellationToken);

            if (dto == null)
                throw new NotFoundException("User group not found.");

            return dto;
        }
    }
}
