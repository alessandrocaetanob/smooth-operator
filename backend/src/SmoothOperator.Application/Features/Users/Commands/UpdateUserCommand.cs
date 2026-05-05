using System;
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
    public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Dto, ClaimsPrincipal User) : IRequest<UserListItemDto>;

    public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserListItemDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public UpdateUserCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserListItemDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            user.Name = request.Dto.Name.Trim();
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.updated", "User", user.Id.ToString(), new { user.Name });

            return GetUsersQueryHandler.Project(user);
        }
    }
}
