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
        DbSet<SsoProvider> SsoProviders { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
