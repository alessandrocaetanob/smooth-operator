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

namespace SmoothOperator.Application.Features.ConnectionGroups.Commands
{
    public sealed record UpdateVaultCommand(Guid Id, CreateConnectionGroupDto Dto, ClaimsPrincipal User)
        : IRequest<bool>;

    public sealed class UpdateVaultCommandHandler : IRequestHandler<UpdateVaultCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public UpdateVaultCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(UpdateVaultCommand request, CancellationToken cancellationToken)
        {
            if (request.Dto.ParentGroupId == request.Id)
                throw new BadRequestException("A vault cannot be its own parent.");

            if (request.Dto.ParentGroupId.HasValue)
            {
                var parentExists = await _context.ConnectionGroups
                    .AnyAsync(g => g.Id == request.Dto.ParentGroupId.Value, cancellationToken);
                if (!parentExists)
                    throw new BadRequestException("Parent vault does not exist.");
            }

            var vault = await _context.ConnectionGroups
                .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
            if (vault == null)
                throw new NotFoundException("Vault not found.");

            var name = request.Dto.Name.Trim();
            if (!string.Equals(vault.Name, name, StringComparison.OrdinalIgnoreCase)
                && await VaultNameExistsAsync(name, request.Id, cancellationToken))
            {
                throw new ConflictException($"A vault named \"{name}\" already exists.");
            }

            vault.Name = name;
            vault.ParentGroupId = request.Dto.ParentGroupId;

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("vault.updated", "ConnectionGroup", request.Id.ToString(),
                new { vault.Name, vault.ParentGroupId });

            return true;
        }

        private async Task<bool> VaultNameExistsAsync(string name, Guid? excludeId, CancellationToken ct)
        {
            var query = _context.ConnectionGroups.AsNoTracking()
                .Where(v => v.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue) query = query.Where(v => v.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }
    }
}
