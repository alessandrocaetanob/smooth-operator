using SmoothOperator.Application.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Infrastructure.Services.Sso
{
    public record SsoProvisioningResult(User User, string Token, bool WasCreated, bool WasLinked);

    public interface ISsoUserProvisioningService
    {
        /// <summary>
        /// JIT-provisions or links a local user from an SSO assertion/ID token.
        /// Lookup order: ExternalId → Email (link existing local user) → create new with default User role.
        /// Throws <see cref="UnauthorizedAccessException"/> when the matched user is disabled.
        /// </summary>
        Task<SsoProvisioningResult> ProvisionOrLinkAsync(SsoProviderType providerType, string externalId, string email, string? displayName);
    }

    public class SsoUserProvisioningService : ISsoUserProvisioningService
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokens;
        private readonly IAuditService _audit;

        public SsoUserProvisioningService(AppDbContext db, ITokenService tokens, IAuditService audit)
        {
            _db = db;
            _tokens = tokens;
            _audit = audit;
        }

        public async Task<SsoProvisioningResult> ProvisionOrLinkAsync(SsoProviderType providerType, string externalId, string email, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(externalId))
                throw new ArgumentException("externalId is required", nameof(externalId));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("email is required", nameof(email));

            email = email.Trim().ToLowerInvariant();

            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.ExternalId == externalId);

            var wasCreated = false;
            var wasLinked = false;

            if (user == null)
            {
                user = await _db.Users
                    .Include(u => u.Roles)
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user != null)
                {
                    user.ExternalId = externalId;
                    user.SsoProviderType = providerType;
                    wasLinked = true;
                }
                else
                {
                    var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == AppRoles.User)
                        ?? throw new InvalidOperationException("Default 'User' role missing — seeder did not run.");

                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        Name = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                        ExternalId = externalId,
                        SsoProviderType = providerType,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    user.Roles.Add(defaultRole);
                    _db.Users.Add(user);
                    wasCreated = true;
                }
            }

            if (!user.IsActive)
            {
                await _audit.WriteAsync("sso.login_failed", "user", user.Id.ToString(),
                    new { reason = "user_disabled", providerType = providerType.ToString() });
                throw new UnauthorizedAccessException("User account is disabled.");
            }

            await _db.SaveChangesAsync();

            var defaultAction = wasLinked ? "user.sso_linked" : "user.sso_login";
            var action = wasCreated ? "user.sso_provisioned" : defaultAction;
            await _audit.WriteAsync(action, "user", user.Id.ToString(),
                new { providerType = providerType.ToString(), externalId, email });

            var token = _tokens.CreateToken(user);
            return new SsoProvisioningResult(user, token, wasCreated, wasLinked);
        }
    }
}
