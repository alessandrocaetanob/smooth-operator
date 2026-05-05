using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Helpers;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Auth.Commands
{
    public sealed record SetupCommand(string Email, string Name, string Password) : IRequest<AuthResponse>;

    public sealed class SetupCommandHandler : IRequestHandler<SetupCommand, AuthResponse>
    {
        private readonly IAppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IAuditService _audit;

        public SetupCommandHandler(
            IAppDbContext context,
            ITokenService tokenService,
            IAuditService audit)
        {
            _context = context;
            _tokenService = tokenService;
            _audit = audit;
        }

        public async Task<AuthResponse> Handle(SetupCommand request, CancellationToken cancellationToken)
        {
            if (await _context.Users.AnyAsync(cancellationToken))
                throw new ConflictException("Setup has already been completed.");

            var email = request.Email.Trim().ToLowerInvariant();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var ownerRole = await RequireRoleAsync(
                "Owner",
                "Root access. First account created during setup.",
                cancellationToken);
            user.Roles.Add(ownerRole);

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("user.bootstrap", "User", user.Id.ToString(),
                new { provider = "local", role = "Owner" });

            return UserHelper.ToAuthResponse(user, _tokenService);
        }

        private async Task<Role> RequireRoleAsync(string roleName, string description, CancellationToken ct)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role != null) return role;

            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                Description = description
            };
            _context.Roles.Add(role);
            return role;
        }
    }
}
