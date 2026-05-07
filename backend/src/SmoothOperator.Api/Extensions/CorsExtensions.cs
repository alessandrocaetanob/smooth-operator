using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmoothOperator.Api.Extensions;

public static class CorsExtensions
{
    public const string CorsPolicyName = "AllowConfiguredOrigins";

    // Allowed origins are read from AppUrls:Frontend and AppUrls:AllowedOrigins[]
    // so the policy adjusts to the deployment environment without code changes.
    // Set AppUrls__AllowedOrigins__0, __1, … environment variables to add origins.
    public static IServiceCollection AddApplicationCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appUrlsCfg = configuration.GetSection("AppUrls");
        var frontendOrigin = appUrlsCfg["Frontend"];
        var appOrigin = appUrlsCfg["App"];
        var extraOrigins = appUrlsCfg.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                var origins = new List<string>();
                if (!string.IsNullOrWhiteSpace(frontendOrigin))
                    origins.Add(frontendOrigin.TrimEnd('/'));
                if (!string.IsNullOrWhiteSpace(appOrigin) && appOrigin != frontendOrigin)
                    origins.Add(appOrigin.TrimEnd('/'));
                origins.AddRange(extraOrigins
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.TrimEnd('/')));

                if (origins.Count > 0)
                    policy.WithOrigins([.. origins])
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                else
                    // No origins configured — permissive dev fallback (no credentials).
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }
}
