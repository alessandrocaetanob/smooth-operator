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
                await RoleSeeder.SeedDefaultsAsync(db, logger);
                return;
            }

            if (!IsPostgres(db))
            {
                await ApplyPendingAsync(db, logger);
                await RoleSeeder.SeedDefaultsAsync(db, logger);
                return;
            }

            // Pin one connection for the entire lock → migrate → seed → unlock sequence.
            // pg_advisory_lock is session-scoped; without pinning, EF's pool can return the
            // connection between calls. Another replica then reuses that session and sees the
            // lock as already held by itself, making it reentrant instead of exclusive.
            // RoleSeeder runs inside the lock so replicas can't race to insert the same roles.
            await db.Database.OpenConnectionAsync();
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0});", MigrationLockId);
                await ApplyPendingAsync(db, logger);
                await RoleSeeder.SeedDefaultsAsync(db, logger);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0});", MigrationLockId);
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
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
