using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Users.Commands
{
    public sealed record DeleteUserCommand(Guid UserId, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteUserCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var idClaim = request.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idClaim, out var meId) && meId == request.UserId)
                throw new ConflictException("You cannot delete your own account.");

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            if (user.Roles.Any(r => r.Name.Equals("Owner", StringComparison.OrdinalIgnoreCase)))
            {
                var otherOwners = await _context.Users
                    .Include(u => u.Roles)
                    .CountAsync(u => u.Id != request.UserId
                        && u.Roles.Any(r => r.Name == "Owner"), cancellationToken);

                if (otherOwners == 0)
                    throw new ConflictException("Cannot delete the last Owner.");
            }

            var email = user.Email;
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.deleted", "User", request.UserId.ToString(), new { email });

            return true;
        }
    }
}
