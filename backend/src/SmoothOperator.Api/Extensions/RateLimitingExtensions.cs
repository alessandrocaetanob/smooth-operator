using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using IPNetwork = System.Net.IPNetwork;

namespace SmoothOperator.Api.Extensions;

public static class RateLimitingExtensions
{
    // Read limits directly from config at startup (same values registered in IOptions<RateLimitOptions>).
    public static IServiceCollection AddApplicationRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rlCfg = configuration.GetSection("RateLimit");
        var globalPermit = rlCfg.GetValue<int>("Global:PermitLimit", 100);
        var globalWindow = rlCfg.GetValue<int>("Global:WindowSeconds", 60);
        var authPermit = rlCfg.GetValue<int>("Auth:PermitLimit", 5);
        var authWindow = rlCfg.GetValue<int>("Auth:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = globalPermit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(globalWindow)
                    }));

            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = authPermit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(authWindow)
                    }));
        });

        return services;
    }

    // Trust the X-Forwarded-For / X-Forwarded-Proto headers sent by the nginx reverse-proxy
    // container so rate-limiting partitions by the real client IP.
    public static IServiceCollection AddApplicationForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
        });
        return services;
    }
}
