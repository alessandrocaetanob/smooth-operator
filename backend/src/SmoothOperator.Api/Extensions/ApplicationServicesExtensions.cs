using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.SecretProviders;
using SmoothOperator.Infrastructure.Services.Sso;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace SmoothOperator.Api.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddDbContextPool<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Redis — registered as singleton IConnectionMultiplexer so GuacamoleProxyService
        // and other consumers share one multiplexed connection rather than each opening one.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis") ?? "localhost:6379"));

        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<GuacamoleProxyService>();
        services.AddSingleton<IAppMetrics, AppMetrics>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddHostedService<AuditRetentionService>();

        // SSO services
        services.AddScoped<ISsoProviderService, SsoProviderService>();
        services.AddScoped<IOidcFlowService, OidcFlowService>();
        services.AddScoped<ISamlFlowService, SamlFlowService>();
        services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
        services.AddScoped<ISsoConnectionTester, SsoConnectionTester>();
        services.AddScoped<SsoUrlHelper>();
        services.AddHttpClient();

        // Secret provider factory (transient — each call creates a live SDK client for that provider config)
        services.AddTransient<ISecretProviderFactory, SecretProviderFactory>();

        return services;
    }

    // Persist ASP.NET Core data-protection keys to a named-volume-backed directory so
    // encrypted cookies / antiforgery tokens survive container restarts.
    // Override the path via DataProtection__KeysPath or the DataProtection:KeysPath
    // config key (default: /data/protection-keys, which maps to the dp_keys volume).
    public static IServiceCollection AddApplicationDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dpKeysPath = configuration["DataProtection:KeysPath"] ?? "/data/protection-keys";
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
            .SetApplicationName("smooth-operator");
        return services;
    }

    public static IServiceCollection AddApplicationHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("DefaultConnection") ?? "")
            .AddRedis(configuration.GetConnectionString("Redis") ?? "localhost:6379");
        return services;
    }
}
