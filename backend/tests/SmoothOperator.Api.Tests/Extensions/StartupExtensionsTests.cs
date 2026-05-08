using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    public void AddApplicationCors_NoOriginsConfigured_Development_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        var exception = Record.Exception(() => services.AddApplicationCors(configuration, environment.Object));

        Assert.Null(exception);
    }
}
