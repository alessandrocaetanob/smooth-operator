using SmoothOperator.Application.Options;

namespace SmoothOperator.Api.Extensions;

public static class OptionsExtensions
{
    // Most options classes are registered with DataAnnotations validation and
    // ValidateOnStart() so misconfiguration is caught at startup, not at first request.
    // MetricsOptions is intentionally excluded because BearerToken is optional in Development/Testing.
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration("Jwt")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GuacdOptions>()
            .BindConfiguration("Guacd")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EncryptionOptions>()
            .BindConfiguration("Encryption")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OtelOptions>()
            .BindConfiguration("Otel")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AppUrlsOptions>()
            .BindConfiguration("AppUrls")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .BindConfiguration("RateLimit")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .BindConfiguration("Auth")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // BearerToken is intentionally optional — no ValidateOnStart() so dev environments
        // can run without configuring it. Production should always set Metrics__BearerToken.
        services.AddOptions<MetricsOptions>()
            .BindConfiguration(MetricsOptions.SectionName);

        return services;
    }
}
