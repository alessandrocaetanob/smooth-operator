using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Auth.Commands
{
    public sealed record InviteUserCommand(
        string Email,
        string? Name,
        string? Role,
        Guid? AdminId) : IRequest<InviteUserResult>;

    public sealed record InviteUserResult(
        string Message,
        string InviteUrl,
        bool EmailSent,
        string? EmailError);

    public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, InviteUserResult>
    {
        private readonly IAppDbContext _context;
        private readonly IInviteService _inviteService;
        private readonly IEmailService _emailService;
        private readonly IAuditService _audit;

        public InviteUserCommandHandler(
            IAppDbContext context,
            IInviteService inviteService,
            IEmailService emailService,
            IAuditService audit)
        {
            _context = context;
            _inviteService = inviteService;
            _emailService = emailService;
            _audit = audit;
        }

        public async Task<InviteUserResult> Handle(InviteUserCommand request, CancellationToken cancellationToken)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
                throw new ConflictException("User already exists.");

            string requestedRole;
            if (string.IsNullOrWhiteSpace(request.Role))
            {
                requestedRole = "User";
            }
            else
            {
                var knownRoles = new[] { "Owner", "Admin", "TeamAdmin", "User" };
                var matched = Array.Find(knownRoles, r => string.Equals(r, request.Role, StringComparison.OrdinalIgnoreCase));
                if (matched == null)
                    throw new BadRequestException($"Unknown role \"{request.Role}\".");

                requestedRole = matched;
            }

            if (string.Equals(requestedRole, "Owner", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Owner role cannot be assigned via invite.");

            var roleDescription = requestedRole switch
            {
                "Admin" => "Can create groups, invite users, vaults and credentials.",
                "TeamAdmin" => "Can create/manage connections in assigned vaults.",
                _ => "Can use connections in assigned vaults."
            };
            var role = await RequireRoleAsync(requestedRole, roleDescription, cancellationToken);

            var displayName = string.IsNullOrWhiteSpace(request.Name)
                ? request.Email
                : request.Name.Trim();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = displayName,
                IsActive = false
            };
            user.Roles.Add(role);
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            var (_, _, inviteUrl) = await _inviteService.CreateAsync(
                user.Id, IInviteService.TypeUserInvite, TimeSpan.FromHours(72), request.AdminId);

            var emailSent = false;
            string? emailError = null;
            if (await _emailService.IsConfiguredAsync())
            {
                var result = await _emailService.SendInviteAsync(user.Email, user.Name, inviteUrl);
                emailSent = result.Success;
                emailError = result.Error;
            }

            await _audit.WriteAsync("user.invited", "User", user.Id.ToString(), new
            {
                email = user.Email,
                name = user.Name,
                role = requestedRole,
                emailSent
            });

            return new InviteUserResult("User invited successfully.", inviteUrl, emailSent, emailError);
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
