using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.ConnectionGroups.Commands
{
    public sealed record CreateVaultCommand(CreateConnectionGroupDto Dto, ClaimsPrincipal User)
        : IRequest<ConnectionGroupDto>;

    public sealed class CreateVaultCommandHandler : IRequestHandler<CreateVaultCommand, ConnectionGroupDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public CreateVaultCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<ConnectionGroupDto> Handle(CreateVaultCommand request, CancellationToken cancellationToken)
        {
            if (request.Dto.ParentGroupId.HasValue)
            {
                var parentExists = await _context.ConnectionGroups
                    .AnyAsync(g => g.Id == request.Dto.ParentGroupId.Value, cancellationToken);
                if (!parentExists)
                    throw new BadRequestException("Parent vault does not exist.");
            }

            var name = request.Dto.Name.Trim();
            if (await VaultNameExistsAsync(name, null, cancellationToken))
                throw new ConflictException($"A vault named \"{name}\" already exists.");

            var vault = new ConnectionGroup
            {
                Id = Guid.NewGuid(),
                Name = name,
                ParentGroupId = request.Dto.ParentGroupId,
                RecordingEnabled = request.Dto.RecordingEnabled,
                RecordingIncludeKeys = request.Dto.RecordingIncludeKeys,
                RecordingRetentionDays = request.Dto.RecordingRetentionDays,
                FileTransferPolicy = request.Dto.FileTransferPolicy,
            };

            _context.ConnectionGroups.Add(vault);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("vault.created", "ConnectionGroup", vault.Id.ToString(),
                new { vault.Name, vault.ParentGroupId, vault.RecordingEnabled });

            return new ConnectionGroupDto
            {
                Id = vault.Id,
                Name = vault.Name,
                ParentGroupId = vault.ParentGroupId,
                RecordingEnabled = vault.RecordingEnabled,
                RecordingIncludeKeys = vault.RecordingIncludeKeys,
                RecordingRetentionDays = vault.RecordingRetentionDays,
                FileTransferPolicy = vault.FileTransferPolicy,
            };
        }

        private async Task<bool> VaultNameExistsAsync(string name, Guid? excludeId, CancellationToken ct)
        {
            var lowerName = name.ToLower();
            var query = _context.ConnectionGroups.AsNoTracking()
                .Where(v => v.Name.ToLower() == lowerName);
            if (excludeId.HasValue) query = query.Where(v => v.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }
    }
}
