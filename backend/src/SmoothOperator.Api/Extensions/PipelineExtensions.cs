using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

        var metricsOptions = app.Services.GetRequiredService<IOptions<MetricsOptions>>().Value;
        EnsureMetricsBearerTokenConfigured(app, metricsOptions);

        // Guard the Prometheus scrape endpoint with a bearer token when configured.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/metrics"),
            branch => branch.Use(async (ctx, next) =>
            {
                if (HasValidMetricsBearerToken(ctx, metricsOptions.BearerToken))
                {
                    ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("Prometheus"));
                }
                else
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                await next(ctx);
            }));

        app.UseAuthorization();
        app.MapControllers();

        // Liveness: process is up. No checks executed — if the endpoint responds, we're alive.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        // Readiness: tagged checks (DB + Redis) must succeed for the app to accept traffic.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckJson,
        });

        // Backwards-compat: legacy /health route stays pointed at readiness.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        app.MapMetrics("/metrics").RequireAuthorization();

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

    private static Task WriteHealthCheckJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString().ToLowerInvariant(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload), context.RequestAborted);
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
