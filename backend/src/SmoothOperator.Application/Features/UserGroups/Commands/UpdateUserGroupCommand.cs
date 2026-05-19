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

namespace SmoothOperator.Application.Features.UserGroups.Commands
{
    public sealed record UpdateUserGroupCommand(Guid Id, RenameUserGroupRequest Dto, ClaimsPrincipal User)
        : IRequest<UserGroupDto>;

    public sealed class UpdateUserGroupCommandHandler : IRequestHandler<UpdateUserGroupCommand, UserGroupDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public UpdateUserGroupCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserGroupDto> Handle(UpdateUserGroupCommand request, CancellationToken cancellationToken)
        {
            var group = await _context.UserGroups
                .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
            if (group == null)
                throw new NotFoundException("User group not found.");

            var name = request.Dto.Name.Trim();
            var previousName = group.Name;
            if (!string.Equals(previousName, name, StringComparison.OrdinalIgnoreCase)
                && await NameExistsAsync(name, request.Id, cancellationToken))
            {
                throw new ConflictException($"A group named \"{name}\" already exists.");
            }

            if (request.Dto.OwnerUserId.HasValue)
            {
                var ownerExists = await _context.Users.AnyAsync(u => u.Id == request.Dto.OwnerUserId.Value, cancellationToken);
                if (!ownerExists)
                    throw new BadRequestException("Owner user does not exist.");
            }

            var previousDescription = group.Description;
            var previousOwnerId = group.OwnerUserId;

            group.Name = name;
            group.Description = string.IsNullOrWhiteSpace(request.Dto.Description) ? null : request.Dto.Description.Trim();
            group.OwnerUserId = request.Dto.OwnerUserId;

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("group.updated", "UserGroup", request.Id.ToString(),
                new { previousName, newName = group.Name, previousDescription, newDescription = group.Description, previousOwnerId, newOwnerId = group.OwnerUserId });

            return await BuildGroupDtoAsync(group.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to load group after update.");
        }

        private async Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct)
        {
            var lowerName = name.ToLower();
            var query = _context.UserGroups.AsNoTracking()
                .Where(g => g.Name.ToLower() == lowerName);
            if (excludeId.HasValue) query = query.Where(g => g.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        private Task<UserGroupDto?> BuildGroupDtoAsync(Guid id, CancellationToken ct)
            => CreateUserGroupCommandHandler.BuildGroupDtoAsync(id, _context, ct);
    }
}
