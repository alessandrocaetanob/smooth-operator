using System;
using System.Linq;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Helpers
{
    public static class UserHelper
    {
        public static string? BuildAvatarUrl(User user)
        {
            if (string.IsNullOrWhiteSpace(user.AvatarBase64) ||
                string.IsNullOrWhiteSpace(user.AvatarMimeType))
            {
                return null;
            }
            return $"data:{user.AvatarMimeType};base64,{user.AvatarBase64}";
        }

        public static AuthResponse ToAuthResponse(User user, ITokenService tokenService)
            => new()
            {
                Token = tokenService.CreateToken(user),
                ExpiresAt = DateTime.UtcNow.Add(tokenService.TokenLifetime),
                User = ToUserInfo(user)
            };

        public static UserInfo ToUserInfo(User user)
            => new()
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
                SsoProviderType = user.SsoProviderType?.ToString(),
                AvatarUrl = BuildAvatarUrl(user),
                Roles = user.Roles
                    .Select(r => r.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList()
            };
    }
}
