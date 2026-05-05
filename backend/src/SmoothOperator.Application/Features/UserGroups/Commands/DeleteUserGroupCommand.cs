using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.UserGroups.Commands
{
    public sealed record DeleteUserGroupCommand(Guid Id, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class DeleteUserGroupCommandHandler : IRequestHandler<DeleteUserGroupCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteUserGroupCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteUserGroupCommand request, CancellationToken cancellationToken)
        {
            var group = await _context.UserGroups
                .Include(g => g.Vaults)
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

            if (group == null)
                throw new NotFoundException("User group not found.");

            if (group.Vaults.Count > 0)
                throw new ConflictException("Cannot delete a group that is assigned to vaults. Remove vault assignments first.");

            var snapshot = new { group.Name, MemberCount = group.Members.Count };
            _context.UserGroups.Remove(group);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("group.deleted", "UserGroup", request.Id.ToString(), snapshot);

            return true;
        }
    }
}
