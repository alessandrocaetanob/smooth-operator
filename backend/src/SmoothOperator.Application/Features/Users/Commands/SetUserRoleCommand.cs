using System;
using System.Collections.Generic;
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
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Users.Commands
{
    public sealed record SetUserRoleCommand(Guid UserId, SetUserRoleRequest Dto, ClaimsPrincipal User) : IRequest<UserListItemDto>;

    public sealed class SetUserRoleCommandHandler : IRequestHandler<SetUserRoleCommand, UserListItemDto>
    {
        private const string RoleOwner = "Owner";
        private static readonly string[] ValidRoles = { RoleOwner, "Admin", "TeamAdmin", "User" };

        private static readonly IReadOnlyDictionary<string, string> RoleDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Owner"] = "Root access and bootstrap account owner.",
                ["Admin"] = "Can create groups, invite users, vaults and credentials.",
                ["TeamAdmin"] = "Can create/manage connections in assigned vaults.",
                ["User"] = "Can only use connections in assigned vaults."
            };

        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public SetUserRoleCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserListItemDto> Handle(SetUserRoleCommand request, CancellationToken cancellationToken)
        {
            var normalizedRole = ValidRoles.FirstOrDefault(r =>
                r.Equals(request.Dto.Role, StringComparison.OrdinalIgnoreCase));

            if (normalizedRole == null)
                throw new BadRequestException($"Unsupported role. Allowed values: {string.Join(", ", ValidRoles)}.");

            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            var wasOwner = user.Roles.Any(r => r.Name.Equals(RoleOwner, StringComparison.OrdinalIgnoreCase));
            var becomesOwner = string.Equals(normalizedRole, RoleOwner, StringComparison.OrdinalIgnoreCase);

            if (wasOwner && !becomesOwner && user.IsActive)
            {
                var otherActiveOwners = await _context.Users
                    .Include(u => u.Roles)
                    .CountAsync(u => u.Id != request.UserId && u.IsActive
                        && u.Roles.Any(r => r.Name == RoleOwner), cancellationToken);

                if (otherActiveOwners == 0)
                    throw new ConflictException("Cannot remove the Owner role from the last active Owner.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == normalizedRole, cancellationToken);
            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = normalizedRole,
                    Description = RoleDescriptions[normalizedRole]
                };
                _context.Roles.Add(role);
            }

            user.Roles.Clear();
            user.Roles.Add(role);

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.role_changed", "User", user.Id.ToString(), new { role = normalizedRole });

            return GetUsersQueryHandler.Project(user);
        }
    }
}
