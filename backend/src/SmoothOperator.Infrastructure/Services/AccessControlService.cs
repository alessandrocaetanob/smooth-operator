using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Infrastructure.Services
{
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
