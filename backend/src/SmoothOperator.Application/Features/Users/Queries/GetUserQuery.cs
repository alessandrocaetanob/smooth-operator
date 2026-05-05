using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Users.Queries
{
    public sealed record GetUserQuery(Guid Id, ClaimsPrincipal User) : IRequest<UserListItemDto>;

    public sealed class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserListItemDto>
    {
        private readonly IAppDbContext _context;

        public GetUserQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<UserListItemDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            return GetUsersQueryHandler.Project(user);
        }
    }
}
