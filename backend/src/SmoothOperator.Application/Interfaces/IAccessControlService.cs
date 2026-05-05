using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    public sealed class AccessProfile
    {
        public Guid UserId { get; init; }
        public string Email { get; init; } = string.Empty;
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
        public IReadOnlyList<Guid> VaultIds { get; init; } = Array.Empty<Guid>();

        public bool IsOwner => HasRole("Owner");
        public bool IsAdmin => HasRole("Admin");
        public bool IsOwnerOrAdmin => IsOwner || IsAdmin;
        public bool IsTeamAdmin => HasRole("TeamAdmin");
        public bool IsDefaultUser => HasRole("User");

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
}
