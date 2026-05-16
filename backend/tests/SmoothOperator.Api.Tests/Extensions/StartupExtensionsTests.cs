using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using SmoothOperator.Api.Extensions;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace SmoothOperator.Api.Tests.Extensions;

public class StartupExtensionsTests
{
    [Fact]
    public void AddJwtAuthentication_WithoutJwtKey_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddJwtAuthentication(configuration));
        Assert.Contains("Jwt:Key is not configured", ex.Message);
    }

    [Fact]
    public void AddJwtAuthentication_UsesDefaultIssuerAndAudience_WhenMissingInConfig()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-only-signing-key-not-used-for-anything-real-please-32+chars"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var options = monitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal(TokenService.LocalIssuer, options.TokenValidationParameters.ValidIssuer);
        Assert.Equal(TokenService.LocalAudience, options.TokenValidationParameters.ValidAudience);
    }

    [Fact]
    public async Task ApplyPendingMigrationsAsync_NonRelational_EnsuresCreatedAndSeedsRoles()
    {
        var dbName = $"migration-test-{Guid.NewGuid():N}";
        var dbRoot = new InMemoryDatabaseRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddLogging();
        builder.Services.AddDbContext<AppDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName, dbRoot));

        await using var app = builder.Build();

        await app.ApplyPendingMigrationsAsync();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = await db.Roles.Select(r => r.Name).ToListAsync();

        Assert.Contains(AppRoles.Owner, roles);
        Assert.Contains(AppRoles.Admin, roles);
        Assert.Contains(AppRoles.TeamAdmin, roles);
        Assert.Contains(AppRoles.User, roles);
    }

    [Fact]
    public void AddApplicationCors_NoOriginsConfigured_NonDevelopment_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Production");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddApplicationCors(configuration, environment.Object));

        Assert.Contains("CORS is not configured", ex.Message);
    }

    [Fact]
    public void AddApplicationCors_NoOriginsConfigured_Development_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddApplicationCors(configuration, environment.Object));

        Assert.Contains("CORS is not configured", ex.Message);
    }

    [Fact]
    public void AddApplicationDataProtection_DefaultPath_RegistersDataProtection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DataProtection:KeysPath"] = tempDir })
                .Build();

            services.AddApplicationDataProtection(config);

            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IDataProtectionProvider>());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddApplicationDataProtection_WithNonExistentCertPath_SkipsCertProtection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeysPath"] = tempDir,
                    ["DataProtection:CertPath"] = Path.Combine(tempDir, "does-not-exist.pfx")
                })
                .Build();

            // File.Exists guard should prevent cert loading — no exception expected.
            services.AddApplicationDataProtection(config);

            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IDataProtectionProvider>());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddApplicationDataProtection_WithValidCertPath_RegistersCertProtection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var certPath = Path.Combine(tempDir, "dp-test.pfx");
        try
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=smooth-operator-test", rsa,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
            File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx));

            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeysPath"] = tempDir,
                    ["DataProtection:CertPath"] = certPath
                })
                .Build();

            services.AddApplicationDataProtection(config);

            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IDataProtectionProvider>());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPendingMigrationsAsync_RelationalDb_ThrowsAfterMaxRetries()
    {
        // SQLite is relational (IsRelational() == true), but cannot run the
        // PostgreSQL-specific migrations — MigrateAsync() throws on every attempt,
        // exercising the retry catch block and the final rethrow.
        var dbPath = $"DataSource=file:retry-test-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddLogging();
        builder.Services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(dbPath));

        await using var app = builder.Build();

        await Assert.ThrowsAnyAsync<Exception>(
            () => app.ApplyPendingMigrationsAsync(retryDelay: _ => TimeSpan.Zero));
    }
}
