using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.ConnectionGroups.Commands
{
    public sealed record DeleteVaultCommand(Guid Id, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class DeleteVaultCommandHandler : IRequestHandler<DeleteVaultCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteVaultCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteVaultCommand request, CancellationToken cancellationToken)
        {
            var vault = await _context.ConnectionGroups
                .Include(v => v.Connections)
                .Include(v => v.Users)
                .Include(v => v.Groups)
                .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

            if (vault == null)
                throw new NotFoundException("Vault not found.");

            if (vault.Connections.Count > 0)
                throw new ConflictException("Cannot delete a vault that still has connections.");
            if (vault.Users.Count > 0)
                throw new ConflictException("Cannot delete a vault that is assigned to users.");
            if (vault.Groups.Count > 0)
                throw new ConflictException("Cannot delete a vault that is assigned to groups.");

            _context.ConnectionGroups.Remove(vault);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("vault.deleted", "ConnectionGroup", request.Id.ToString(),
                new { vault.Name });

            return true;
        }
    }
}
