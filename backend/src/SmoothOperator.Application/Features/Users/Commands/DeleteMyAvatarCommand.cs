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
using SmoothOperator.Application.Helpers;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Users.Commands
{
    public sealed record DeleteMyAvatarCommand(ClaimsPrincipal User) : IRequest<UserInfo>;

    public sealed class DeleteMyAvatarCommandHandler : IRequestHandler<DeleteMyAvatarCommand, UserInfo>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteMyAvatarCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserInfo> Handle(DeleteMyAvatarCommand request, CancellationToken cancellationToken)
        {
            var idClaim = request.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var meId))
                throw new UnauthorizedException("Not authenticated.");

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == meId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            user.AvatarBase64 = null;
            user.AvatarMimeType = null;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.avatar_removed", "User", user.Id.ToString(), new { });

            return new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
                SsoProviderType = user.SsoProviderType?.ToString(),
                AvatarUrl = null,
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            };
        }
    }
}
