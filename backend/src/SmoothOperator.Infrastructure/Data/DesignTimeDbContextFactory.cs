using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmoothOperator.Infrastructure.Data
{
    // Used only by `dotnet ef` design-time tooling so migrations can be created
    // without spinning up the full application host.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
#pragma warning disable S2068 // design-time only — not a production credential
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=smoothoperator;Username=postgres;Password=postgres")
                .Options;
#pragma warning restore S2068
            return new AppDbContext(options);
        }
    }
}
