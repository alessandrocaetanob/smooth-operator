using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmoothOperator.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real ASP.NET Core pipeline with two test-only swaps:
/// 1. Replaces the Npgsql DbContext with a fresh InMemory store (one per factory).
/// 2. Replaces the JWT auth handler with <see cref="TestAuthHandler"/> so tests
///    can impersonate users via request headers.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"test-db-{Guid.NewGuid():N}";

    // A valid 64-character hex key (32 bytes for AES-256) used only in tests.
    private const string TestEncryptionKey = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    private readonly Action<AppDbContext>? _seed;
    private readonly bool _seedPhantomOwner;

    static TestWebApplicationFactory()
    {
        // These must be set BEFORE Program.Main runs because TokenService and other
        // services are constructed against builder.Configuration before the WAF's
        // ConfigureAppConfiguration hook fires (which only runs at builder.Build()).
        Environment.SetEnvironmentVariable("Jwt__Key", "test-only-signing-key-not-used-for-anything-real-please-32+chars");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "test-issuer");
        Environment.SetEnvironmentVariable("Jwt__Audience", "test-audience");
        Environment.SetEnvironmentVariable("AzureAd__ClientId", "");
        Environment.SetEnvironmentVariable("AzureAd__TenantId", "");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Host=localhost;Database=tests;Username=test;Password=test");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        // EncryptionService requires a 64-char hex key (32 bytes for AES-256).
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", TestEncryptionKey);
    }

    public TestWebApplicationFactory(Action<AppDbContext>? seed = null, bool seedPhantomOwner = true)
    {
        _seed = seed;
        _seedPhantomOwner = seedPhantomOwner;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Provide harmless connection strings so health checks register cleanly;
                // they are not invoked during the tests we run here.
                ["ConnectionStrings:DefaultConnection"] = $"Host=localhost;Database={DatabaseName};Username=test;Password=test",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                // Force-disable Entra so the auth pipeline only registers the local JWT handler,
                // which we then replace below.
                ["AzureAd:ClientId"] = "",
                ["AzureAd:TenantId"] = "",
                ["Jwt:Key"] = "test-only-signing-key-not-used-for-anything-real-please-32+chars",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                // EncryptionService requires a 64-char hex key (32 bytes for AES-256).
                ["ENCRYPTION_KEY"] = TestEncryptionKey
            });
        });

        builder.ConfigureServices(services =>
        {
            // Swap DbContext to InMemory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            // Also remove EF Core internal service descriptors that Npgsql registered,
            // otherwise EF complains about two providers in the same container.
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var sd = services[i];
                if (sd.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
                    || sd.ServiceType.FullName?.StartsWith("Npgsql.EntityFrameworkCore", StringComparison.Ordinal) == true)
                {
                    services.RemoveAt(i);
                }
            }

            services.AddDbContext<AppDbContext>(opts =>
            {
                opts.UseInMemoryDatabase(DatabaseName);
            });

            // Swap real health checks for an empty registry to avoid Npgsql/Redis pings.
            services.RemoveAll<HealthCheckService>();
            services.AddHealthChecks();

            // Replace authentication with our test handler. The app sets explicit
            // DefaultAuthenticateScheme/DefaultChallengeScheme to "Bearer", so we
            // must override them too — AddAuthentication("Test") only sets DefaultScheme.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                o.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });

            // Build a one-shot scope to seed the database and register roles.
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            RoleSeeder.SeedDefaultsAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).GetAwaiter().GetResult();

            // Ensure an Owner exists BEFORE the user seed so RoleSeeder (which Program.cs
            // re-runs after the factory) does not auto-promote the first seeded user to
            // Owner via its bootstrap path. Tests exercising "last-Owner" guards opt out
            // by passing seedPhantomOwner: false and seeding their own Owner explicitly.
            if (_seedPhantomOwner)
            {
                var ownerRole = db.Roles.First(r => r.Name == "Owner");
                var phantomOwner = new SmoothOperator.Domain.Models.User
                {
                    Id = Guid.NewGuid(),
                    Email = "phantom-owner@test.local",
                    Name = "phantom-owner",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddYears(-10)
                };
                phantomOwner.Roles.Add(ownerRole);
                db.Users.Add(phantomOwner);
            }

            _seed?.Invoke(db);
            db.SaveChanges();
        });
    }
}

internal static class ServiceCollectionRemoveAllExtensions
{
    public static void RemoveAll<TService>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                services.RemoveAt(i);
            }
        }
    }
}
