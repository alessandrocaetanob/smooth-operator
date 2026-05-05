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
    public sealed record UpdateMyProfileCommand(UpdateProfileRequest Dto, ClaimsPrincipal User) : IRequest<UserInfo>;

    public sealed class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, UserInfo>
    {
        private const int MaxAvatarBytes = 1_048_576;

        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public UpdateMyProfileCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<UserInfo> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
        {
            var idClaim = request.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var meId))
                throw new UnauthorizedException("Not authenticated.");

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == meId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            user.Name = request.Dto.Name.Trim();

            var hasNewAvatar = !string.IsNullOrWhiteSpace(request.Dto.AvatarBase64);
            if (hasNewAvatar)
            {
                if (string.IsNullOrWhiteSpace(request.Dto.AvatarMimeType))
                    throw new BadRequestException("AvatarMimeType is required when AvatarBase64 is set.");

                byte[] decoded;
                try
                {
                    decoded = Convert.FromBase64String(request.Dto.AvatarBase64!);
                }
                catch (FormatException)
                {
                    throw new BadRequestException("AvatarBase64 is not a valid base64 string.");
                }

                if (decoded.Length > MaxAvatarBytes)
                    throw new BadRequestException($"Avatar exceeds the {MaxAvatarBytes / 1024} KB size limit.");

                user.AvatarBase64 = request.Dto.AvatarBase64;
                user.AvatarMimeType = request.Dto.AvatarMimeType;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                hasNewAvatar ? "user.profile_updated_with_avatar" : "user.profile_updated",
                "User",
                user.Id.ToString(),
                new { user.Name, hasAvatar = !string.IsNullOrEmpty(user.AvatarBase64) });

            return new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
                SsoProviderType = user.SsoProviderType?.ToString(),
                AvatarUrl = UserHelper.BuildAvatarUrl(user),
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            };
        }
    }
}
