using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Extensions
{
    internal static class MigrationExtensions
    {
        internal static async Task ApplyPendingMigrationsAsync(
            this WebApplication app,
            Func<int, TimeSpan>? retryDelay = null)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

            const int maxRetries = 5;
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
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
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delay = retryDelay?.Invoke(attempt) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    logger.LogWarning(ex,
                        "Migration attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay);
                }
#pragma warning disable S2139 // Intentionally log before rethrowing for startup diagnostics
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to apply database migrations on startup after {MaxRetries} attempts.", maxRetries);
                    throw;
                }
#pragma warning restore S2139
            }
        }
    }
}
