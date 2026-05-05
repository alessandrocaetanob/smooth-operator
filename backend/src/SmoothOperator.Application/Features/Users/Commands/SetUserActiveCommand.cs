using System;
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

namespace SmoothOperator.Application.Features.Users.Commands
{
    public sealed record SetUserActiveCommand(Guid UserId, SetUserActiveRequest Dto, ClaimsPrincipal User)
        : IRequest<UserListItemDto>;

    public sealed class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand, UserListItemDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public SetUserActiveCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserListItemDto> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            if (!request.Dto.IsActive)
            {
                var targetIsOwner = user.Roles.Any(r => r.Name.Equals("Owner", StringComparison.OrdinalIgnoreCase));
                if (targetIsOwner)
                {
                    var otherActiveOwners = await _context.Users
                        .Include(u => u.Roles)
                        .CountAsync(u => u.Id != request.UserId && u.IsActive
                            && u.Roles.Any(r => r.Name == "Owner"), cancellationToken);

                    if (otherActiveOwners == 0)
                        throw new ConflictException("Cannot disable the last active Owner.");
                }
            }

            user.IsActive = request.Dto.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                request.Dto.IsActive ? "user.enabled" : "user.disabled",
                "User",
                user.Id.ToString());

            return GetUsersQueryHandler.Project(user);
        }
    }
}
