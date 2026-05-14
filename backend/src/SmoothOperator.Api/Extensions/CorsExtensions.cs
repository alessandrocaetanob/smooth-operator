using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SmoothOperator.Api.Extensions;

public static class CorsExtensions
{
    public const string CorsPolicyName = "AllowConfiguredOrigins";

    // Allowed origins are read from AppUrls:Frontend and AppUrls:AllowedOrigins[]
    // so the policy adjusts to the deployment environment without code changes.
    // Set AppUrls__AllowedOrigins__0, __1, … environment variables to add origins.
    public static IServiceCollection AddApplicationCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var appUrlsCfg = configuration.GetSection("AppUrls");
        var frontendOrigin = appUrlsCfg["Frontend"];
        var appOrigin = appUrlsCfg["App"];
        var extraOrigins = appUrlsCfg.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        var origins = new List<string>();
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
            origins.Add(frontendOrigin.TrimEnd('/'));
        if (!string.IsNullOrWhiteSpace(appOrigin) && appOrigin != frontendOrigin)
            origins.Add(appOrigin.TrimEnd('/'));
        origins.AddRange(extraOrigins
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.TrimEnd('/')));

        if (origins.Count == 0)
            throw new InvalidOperationException(
                "CORS is not configured. Set AppUrls__Frontend or AppUrls__AllowedOrigins__0 before running.");

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins([.. origins])
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
