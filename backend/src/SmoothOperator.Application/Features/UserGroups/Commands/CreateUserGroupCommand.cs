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
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.UserGroups.Commands
{
    public sealed record CreateUserGroupCommand(CreateUserGroupRequest Dto, ClaimsPrincipal User)
        : IRequest<UserGroupDto>;

    public sealed class CreateUserGroupCommandHandler : IRequestHandler<CreateUserGroupCommand, UserGroupDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public CreateUserGroupCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserGroupDto> Handle(CreateUserGroupCommand request, CancellationToken cancellationToken)
        {
            var name = request.Dto.Name.Trim();

            if (await NameExistsAsync(name, null, cancellationToken))
                throw new ConflictException($"A group named \"{name}\" already exists.");

            if (request.Dto.OwnerUserId.HasValue)
            {
                var ownerExists = await _context.Users.AnyAsync(u => u.Id == request.Dto.OwnerUserId.Value, cancellationToken);
                if (!ownerExists)
                    throw new BadRequestException("Owner user does not exist.");
            }

            var group = new UserGroup
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(request.Dto.Description) ? null : request.Dto.Description.Trim(),
                OwnerUserId = request.Dto.OwnerUserId
            };

            _context.UserGroups.Add(group);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("group.created", "UserGroup", group.Id.ToString(),
                new { group.Name, group.Description, group.OwnerUserId });

            return await BuildGroupDtoAsync(group.Id, cancellationToken) ?? throw new InvalidOperationException("Failed to load group after creation.");
        }

        private async Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct)
        {
            var lowerName = name.ToLower();
            var query = _context.UserGroups.AsNoTracking()
                .Where(g => g.Name.ToLower() == lowerName);
            if (excludeId.HasValue) query = query.Where(g => g.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        internal static async Task<UserGroupDto?> BuildGroupDtoAsync(Guid id, IAppDbContext context, CancellationToken ct)
        {
            return await context.UserGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .Include(g => g.Owner)
                .Where(g => g.Id == id)
                .Select(UserGroupProjection.ToDto)
                .FirstOrDefaultAsync(ct);
        }

        private Task<UserGroupDto?> BuildGroupDtoAsync(Guid id, CancellationToken ct)
            => BuildGroupDtoAsync(id, _context, ct);
    }
}
