using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Extensions
{
    internal static class MigrationExtensions
    {
        internal static async Task ApplyPendingMigrationsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
            try
            {
                if (db.Database.IsRelational())
                {
                    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                    if (pending.Count > 0)
                    {
                        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                            pending.Count, string.Join(", ", pending));
                        await db.Database.MigrateAsync();
                        logger.LogInformation("Migrations applied successfully.");
                    }
                    else
                    {
                        logger.LogInformation("Database is up to date; no migrations to apply.");
                    }
                }
                else
                {
                    await db.Database.EnsureCreatedAsync();
                }

                await RoleSeeder.SeedDefaultsAsync(db, logger);
            }
#pragma warning disable S2139 // Intentionally log before rethrowing for startup diagnostics
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply database migrations on startup.");
                throw;
            }
#pragma warning restore S2139
        }
    }
}
