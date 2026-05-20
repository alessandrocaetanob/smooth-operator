using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Api.Extensions
{
    internal static class MigrationExtensions
    {
        // Stable per-application key so only one replica applies migrations at a time
        // on Postgres. Other replicas block on the lock and then find the schema current.
        private const long MigrationLockId = 727355463210L;
        private const int MaxRetries = 5;

        internal static async Task ApplyPendingMigrationsAsync(
            this WebApplication app,
            Func<int, TimeSpan>? retryDelay = null)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await RunMigrationsAsync(db, logger);
                    await RoleSeeder.SeedDefaultsAsync(db, logger);
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    var delay = retryDelay?.Invoke(attempt) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    logger.LogWarning(ex,
                        "Migration attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay}s...",
                        attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay);
                }
#pragma warning disable S2139 // Intentionally log before rethrowing for startup diagnostics
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to apply database migrations on startup after {MaxRetries} attempts.", MaxRetries);
                    throw;
                }
#pragma warning restore S2139
            }
        }

        private static async Task RunMigrationsAsync(AppDbContext db, ILogger logger)
        {
            if (!db.Database.IsRelational())
            {
                await db.Database.EnsureCreatedAsync();
                return;
            }

            var usePostgresLock = IsPostgres(db);
            if (usePostgresLock)
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0});", MigrationLockId);
            }

            try
            {
                await ApplyPendingAsync(db, logger);
            }
            finally
            {
                if (usePostgresLock)
                {
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0});", MigrationLockId);
                }
            }
        }

        private static async Task ApplyPendingAsync(AppDbContext db, ILogger logger)
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Database is up to date; no migrations to apply.");
                return;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));
            }
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully.");
        }

        private static bool IsPostgres(AppDbContext db)
        {
            var provider = db.Database.ProviderName;
            return provider != null && provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
        }
    }
}
