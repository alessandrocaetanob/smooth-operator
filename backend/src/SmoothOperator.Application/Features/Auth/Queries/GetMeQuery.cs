using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Helpers;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Auth.Queries
{
    public sealed record GetMeQuery(Guid UserId) : IRequest<UserInfo>;

    public sealed class GetMeQueryHandler : IRequestHandler<GetMeQuery, UserInfo>
    {
        private readonly IAppDbContext _context;

        public GetMeQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<UserInfo> Handle(GetMeQuery request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            return UserHelper.ToUserInfo(user);
        }
    }
}
