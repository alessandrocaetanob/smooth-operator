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
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=smoothoperator;Username=postgres;Password=postgres")
                .Options;
            return new AppDbContext(options);
        }
    }
}
