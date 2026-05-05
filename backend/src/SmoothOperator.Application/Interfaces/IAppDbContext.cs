using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Role> Roles { get; }
        DbSet<Permission> Permissions { get; }
        DbSet<SmoothOperator.Domain.Models.Host> Hosts { get; }
        DbSet<ConnectionGroup> ConnectionGroups { get; }
        DbSet<Credential> Credentials { get; }
        DbSet<Connection> Connections { get; }
        DbSet<ConnectionTag> ConnectionTags { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<Invitation> Invitations { get; }
        DbSet<SmtpSettings> SmtpSettings { get; }
        DbSet<SystemSettings> SystemSettings { get; }
        DbSet<UserGroup> UserGroups { get; }
        DbSet<SsoProvider> SsoProviders { get; }
        DbSet<SsoAuthState> SsoAuthStates { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
