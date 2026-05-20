using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace SmoothOperator.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real ASP.NET Core pipeline with test-only swaps:
/// 1. Replaces the Npgsql DbContext with a fresh InMemory store (one per factory).
/// 2. Replaces the JWT auth handler with <see cref="TestAuthHandler"/>.
/// 3. Replaces <see cref="IConnectionMultiplexer"/> with a no-op mock (no Redis needed).
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"test-db-{Guid.NewGuid():N}";

    // A valid 64-character hex key (32 bytes for AES-256) used only in tests.
    private const string TestEncryptionKey = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    private readonly Action<AppDbContext>? _seed;
    private readonly bool _seedPhantomOwner;
    private readonly Action<IServiceCollection>? _overrideServices;
    private readonly IReadOnlyDictionary<string, string?>? _overrideConfig;

    static TestWebApplicationFactory()
    {
        // These must be set BEFORE Program.Main runs because options are read
        // from builder.Configuration before the WAF's ConfigureAppConfiguration hook fires.
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
        Environment.SetEnvironmentVariable("Encryption__Key", TestEncryptionKey);
        // CORS fail-closed: provide frontend origin so AddApplicationCors doesn't throw
        // (these must be env vars because they're read before ConfigureAppConfiguration fires).
        Environment.SetEnvironmentVariable("AppUrls__Frontend", "http://localhost:4200");
        Environment.SetEnvironmentVariable("AppUrls__App", "http://localhost:5000");
    }

    public TestWebApplicationFactory(Action<AppDbContext>? seed = null, bool seedPhantomOwner = true, Action<IServiceCollection>? overrideServices = null, IReadOnlyDictionary<string, string?>? overrideConfig = null)
    {
        _seed = seed;
        _seedPhantomOwner = seedPhantomOwner;
        _overrideServices = overrideServices;
        _overrideConfig = overrideConfig;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Connection strings for health checks — not invoked during tests.
                ["ConnectionStrings:DefaultConnection"] = $"Host=localhost;Database={DatabaseName};Username=test;Password=test",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                // Force-disable Entra so only the local JWT handler is registered.
                ["AzureAd:ClientId"] = "",
                ["AzureAd:TenantId"] = "",
                ["Jwt:Key"] = "test-only-signing-key-not-used-for-anything-real-please-32+chars",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                // Options pattern keys (Phase 2).
                ["Encryption:Key"] = TestEncryptionKey,
                ["AppUrls:App"] = "http://localhost:5000",
                ["AppUrls:Frontend"] = "http://localhost:4200",
                // Default to false (invite-only). Tests that need self-register override this explicitly.
                ["Auth:AllowSelfRegister"] = "false",
            });

            if (_overrideConfig is not null)
                cfg.AddInMemoryCollection(_overrideConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Swap DbContext to InMemory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
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

            // Replace the real IConnectionMultiplexer (which tries to connect to Redis)
            // with a no-op mock so tests can run without a running Redis instance.
            services.RemoveAll<IConnectionMultiplexer>();
            var redisMock = new Mock<IConnectionMultiplexer>();
            var dbMock = new Mock<IDatabase>();
            dbMock.Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
                    It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);
            dbMock.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                .Returns(dbMock.Object);
            services.AddSingleton(redisMock.Object);

            // Swap real health checks for an empty registry to avoid Npgsql/Redis pings.
            services.RemoveAll<HealthCheckService>();
            services.AddHealthChecks();

            // Add the test bypass scheme (X-Test-UserId). The real ApiTokenAuthHandler
            // is already registered by prod via AddJwtAuthentication, so we reuse it
            // for "Bearer sop_..." requests via a custom dispatcher policy scheme.
            const string testPolicyScheme = "TestPolicyScheme";
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                .AddPolicyScheme(testPolicyScheme, "TestPolicyScheme", o =>
                {
                    o.ForwardDefaultSelector = ctx =>
                    {
                        var auth = ctx.Request.Headers.Authorization.ToString();
                        if (!string.IsNullOrEmpty(auth) &&
                            auth.StartsWith("Bearer sop_", StringComparison.OrdinalIgnoreCase))
                        {
                            return SmoothOperator.Api.Authentication.ApiTokenAuthHandler.SchemeName;
                        }
                        return TestAuthHandler.SchemeName;
                    };
                });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = testPolicyScheme;
                o.DefaultAuthenticateScheme = testPolicyScheme;
                o.DefaultChallengeScheme = testPolicyScheme;
                o.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });

            // Lock AllowSelfRegister to false by default regardless of which appsettings.*.json
            // is loaded (appsettings.Development.json has it true for local dev convenience).
            // Tests that need self-registration enabled must PostConfigure to true in their own
            // WithWebHostBuilder.ConfigureServices callback.
            services.PostConfigure<SmoothOperator.Application.Options.AuthOptions>(o => o.AllowSelfRegister = false);

            // Build a one-shot scope to seed the database and register roles.
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            RoleSeeder.SeedDefaultsAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).GetAwaiter().GetResult();

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

            _overrideServices?.Invoke(services);
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
