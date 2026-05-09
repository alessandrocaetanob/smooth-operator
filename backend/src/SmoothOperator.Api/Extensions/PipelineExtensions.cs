using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Prometheus;
using Serilog;
using SmoothOperator.Api.Middleware;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseWebSockets();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseForwardedHeaders();

        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(opts =>
        {
            opts.EnrichDiagnosticContext = (diag, ctx) =>
            {
                if (ctx.Items["CorrelationId"] is { } correlationId)
                    diag.Set("CorrelationId", correlationId);
                var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (userId is not null)
                    diag.Set("UserId", userId);
            };
        });

        app.UseResponseCompression();
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors(CorsExtensions.CorsPolicyName);   // Must be after UseRouting, before UseAuthentication/OutputCache
        app.UseOutputCache();
        app.UseHttpMetrics();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        var metricsOptions = app.Services.GetRequiredService<IOptions<MetricsOptions>>().Value;
        EnsureMetricsBearerTokenConfigured(app, metricsOptions);
        ConfigureMetricsEndpoint(app, metricsOptions);

        return app;
    }

    private static void EnsureMetricsBearerTokenConfigured(WebApplication app, MetricsOptions metricsOptions)
    {
        if (app.Environment.IsDevelopment()
            || app.Environment.IsEnvironment("Testing")
            || !string.IsNullOrWhiteSpace(metricsOptions.BearerToken))
        {
            return;
        }

        throw new InvalidOperationException("Metrics:BearerToken must be configured outside Development/Testing environments.");
    }

    private static void ConfigureMetricsEndpoint(WebApplication app, MetricsOptions metricsOptions)
    {
        // Guard the Prometheus scrape endpoint with a bearer token when configured.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/metrics"),
            branch => branch.Use(async (ctx, next) =>
            {
                if (!HasValidMetricsBearerToken(ctx, metricsOptions.BearerToken))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                await next(ctx);
            }));

        app.MapMetrics("/metrics");
    }

    private static bool HasValidMetricsBearerToken(HttpContext context, string? expectedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return true;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        return AuthenticationHeaderValue.TryParse(authHeader, out var parsedAuth)
            && string.Equals(parsedAuth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parsedAuth.Parameter?.Trim(), expectedToken, StringComparison.Ordinal);
    }
}
