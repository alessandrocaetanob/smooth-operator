using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services
{
    public sealed class AccessProfile
    {
        public Guid UserId { get; init; }
        public string Email { get; init; } = string.Empty;
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
        public IReadOnlyList<Guid> VaultIds { get; init; } = Array.Empty<Guid>();

        public bool IsOwner => HasRole(AppRoles.Owner);
        public bool IsAdmin => HasRole(AppRoles.Admin);
        public bool IsOwnerOrAdmin => IsOwner || IsAdmin;
        public bool IsTeamAdmin => HasRole(AppRoles.TeamAdmin);
        public bool IsDefaultUser => HasRole(AppRoles.User);

        public bool HasRole(string roleName)
            => Roles.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
    }

    public interface IAccessControlService
    {
        Task<AccessProfile?> GetCurrentProfileAsync(ClaimsPrincipal principal);
        IQueryable<Connection> ApplyConnectionScope(IQueryable<Connection> query, AccessProfile profile);
        IQueryable<ConnectionGroup> ApplyVaultScope(IQueryable<ConnectionGroup> query, AccessProfile profile);
        bool CanManageConnectionsInVault(AccessProfile profile, Guid? vaultId);
        bool CanUseConnection(AccessProfile profile, Connection connection);
    }

    public class AccessControlService : IAccessControlService
    {
        private readonly AppDbContext _context;

        public AccessControlService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AccessProfile?> GetCurrentProfileAsync(ClaimsPrincipal principal)
        {
            var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var userId))
            {
                return null;
            }

            var raw = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    Roles = u.Roles.Select(r => r.Name).ToList(),
                    DirectVaultIds = u.ConnectionGroups.Select(g => g.Id).ToList(),
                    GroupVaultIds = u.Groups.SelectMany(g => g.Vaults.Select(v => v.Id)).ToList()
                })
                .FirstOrDefaultAsync();

            if (raw == null) return null;

            return new AccessProfile
            {
                UserId = raw.Id,
                Email = raw.Email,
                Roles = raw.Roles,
                VaultIds = raw.DirectVaultIds.Concat(raw.GroupVaultIds).Distinct().ToList()
            };
        }

        public IQueryable<Connection> ApplyConnectionScope(IQueryable<Connection> query, AccessProfile profile)
        {
            if (profile.IsOwnerOrAdmin)
            {
                return query;
            }

            var userId = profile.UserId;
            var vaultIds = profile.VaultIds.ToArray();

            return query.Where(c =>
                c.Users.Any(u => u.Id == userId) ||
                (c.ConnectionGroupId.HasValue && vaultIds.Contains(c.ConnectionGroupId.Value)));
        }

        public IQueryable<ConnectionGroup> ApplyVaultScope(IQueryable<ConnectionGroup> query, AccessProfile profile)
        {
            if (profile.IsOwnerOrAdmin)
            {
                return query;
            }

            var vaultIds = profile.VaultIds.ToArray();
            if (vaultIds.Length == 0)
            {
                return query.Where(_ => false);
            }

            return query.Where(g => vaultIds.Contains(g.Id));
        }

        public bool CanManageConnectionsInVault(AccessProfile profile, Guid? vaultId)
        {
            if (profile.IsOwnerOrAdmin)
            {
                return true;
            }

            if (!profile.IsTeamAdmin || !vaultId.HasValue)
            {
                return false;
            }

            return profile.VaultIds.Contains(vaultId.Value);
        }

        public bool CanUseConnection(AccessProfile profile, Connection connection)
        {
            if (profile.IsOwnerOrAdmin)
            {
                return true;
            }

            if (connection.ConnectionGroupId.HasValue && profile.VaultIds.Contains(connection.ConnectionGroupId.Value))
            {
                return true;
            }

            return connection.Users.Any(u => u.Id == profile.UserId);
        }
    }
}
