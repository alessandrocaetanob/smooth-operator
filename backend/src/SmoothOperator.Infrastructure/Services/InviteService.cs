using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Infrastructure.Services
{
    public class InviteService : IInviteService
    {
        public const string TypeUserInvite = "user_invite";
        public const string TypePasswordReset = "password_reset";

        private readonly AppDbContext _context;
        private readonly AppUrlsOptions _appUrls;

        public InviteService(AppDbContext context, IOptions<AppUrlsOptions> appUrlsOptions)
        {
            _context = context;
            _appUrls = appUrlsOptions.Value;
        }

        public async Task<(Invitation invitation, string token, string url)> CreateAsync(
            Guid userId, string type, TimeSpan ttl, Guid? createdById)
        {
            var raw = GenerateToken();
            var hash = HashToken(raw);

            var invitation = new Invitation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.Add(ttl),
                CreatedById = createdById,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Invitations.Add(invitation);
            await _context.SaveChangesAsync();

            var baseUrl = (!string.IsNullOrEmpty(_appUrls.Frontend)
                ? _appUrls.Frontend
                : (!string.IsNullOrEmpty(_appUrls.App) ? _appUrls.App : "http://localhost:4200"))
                .TrimEnd('/');
            var url = $"{baseUrl}/invite/{raw}";
            return (invitation, raw, url);
        }

        public async Task<Invitation?> ValidateAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var hash = HashToken(token);
            var invitation = await _context.Invitations
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.TokenHash == hash);
            if (invitation == null) return null;
            if (invitation.RedeemedAt != null) return null;
            if (invitation.ExpiresAt < DateTime.UtcNow) return null;
            return invitation;
        }

        public async Task<Invitation?> RedeemAsync(string token)
        {
            var invitation = await ValidateAsync(token);
            if (invitation == null) return null;
            invitation.RedeemedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return invitation;
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }
    }
}
