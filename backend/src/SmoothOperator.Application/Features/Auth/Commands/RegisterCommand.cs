using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Helpers;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Auth.Commands
{
    public sealed record RegisterCommand(string Email, string Name, string Password) : IRequest<AuthResponse>;

    public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
    {
        private readonly IAppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IAuditService _audit;
        private readonly AuthOptions _authOptions;

        public RegisterCommandHandler(
            IAppDbContext context,
            ITokenService tokenService,
            IAuditService audit,
            IOptions<AuthOptions> authOptions)
        {
            _context = context;
            _tokenService = tokenService;
            _audit = audit;
            _authOptions = authOptions.Value;
        }

        public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            if (!_authOptions.AllowSelfRegister)
                throw new NotFoundException("Not found.");

            var email = request.Email.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
                throw new ConflictException("A user with this email already exists.");

            var defaultUserRole = await RequireRoleAsync(
                "User",
                "Can use connections in assigned vaults.",
                cancellationToken);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = request.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.Roles.Add(defaultUserRole);

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("user.registered", "User", user.Id.ToString(),
                new { provider = "local" });

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
