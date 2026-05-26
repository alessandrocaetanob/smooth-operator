using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Application.Options;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Recording;
using SmoothOperator.Infrastructure.Services.SecretProviders;
using SmoothOperator.Infrastructure.Services.Sso;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography.X509Certificates;

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
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql
                    .EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

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
        services.AddSingleton<IWebhookEndpointCache, WebhookEndpointCache>();
        services.AddScoped<IWebhookEnqueuer, WebhookEnqueuer>();
        services.AddScoped<WebhookDeliveryProcessor>();
        services.AddHostedService<WebhookDeliveryService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IMfaService, MfaService>();
        services.AddHostedService<AuditRetentionService>();

        // SSO services
        services.AddScoped<ISsoProviderService, SsoProviderService>();
        services.AddScoped<IOidcFlowService, OidcFlowService>();
        services.AddScoped<ISamlFlowService, SamlFlowService>();
        services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
        services.AddScoped<ISsoConnectionTester, SsoConnectionTester>();
        services.AddScoped<SsoUrlHelper>();
        services.AddHttpClient();

        // Named client for outbound webhook delivery (see WebhookDeliveryService).
        services.AddHttpClient("webhooks", (sp, client) =>
        {
            var webhookOptions = sp.GetRequiredService<IOptions<WebhookOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(webhookOptions.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SmoothOperator-Webhook/1.0");
        });

        // Secret provider factory (transient — each call creates a live SDK client for that provider config)
        services.AddTransient<ISecretProviderFactory, SecretProviderFactory>();

        // Session recording (v1.0.2). Factory is scoped — it opens its own DB scope per call.
        services.AddScoped<IRecordingStorageFactory, RecordingStorageFactory>();
        services.AddScoped<IRecordingUploadService, RecordingUploadService>();
        services.AddHostedService<RecordingRetentionHostedService>();

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
        var builder = services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
            .SetApplicationName("smooth-operator");

        var azureKeyId = configuration["DataProtection:AzureKeyVaultKeyId"];
        var certPath = configuration["DataProtection:CertPath"];

        if (!string.IsNullOrEmpty(azureKeyId))
        {
            // Azure Key Vault wraps the key ring with an Azure-managed key.
            // Set DataProtection__AzureKeyVaultKeyId to the full key identifier URI
            // (e.g. https://my-vault.vault.azure.net/keys/dp-key).
            // Authentication uses DefaultAzureCredential — works with managed identity,
            // environment variables, or the Azure CLI in dev.
            builder.ProtectKeysWithAzureKeyVault(new Uri(azureKeyId), new DefaultAzureCredential());
        }
        else if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
        {
            // Self-hosted: mount a PFX as a Docker secret and set DataProtection__CertPath
            // to the secret path (e.g. /run/secrets/dp_cert.pfx). Optionally set
            // DataProtection__CertPassword if the PFX is password-protected.
            var password = configuration["DataProtection:CertPassword"];
            var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password, X509KeyStorageFlags.EphemeralKeySet);
            builder.ProtectKeysWithCertificate(cert);
        }

        return services;
    }

    public static IServiceCollection AddApplicationHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var hcBuilder = services.AddHealthChecks();
        var pgConnStr = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(pgConnStr))
            hcBuilder.AddNpgSql(pgConnStr, tags: ["ready", "db"]);
        var redisConnStr = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnStr))
            hcBuilder.AddRedis(redisConnStr, tags: ["ready", "redis"]);
        return services;
    }
}
