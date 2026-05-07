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

        // Guard the Prometheus scrape endpoint with an optional bearer token.
        // When Metrics:BearerToken is not configured the endpoint is open (dev mode).
        // In production always set Metrics__BearerToken to a strong random secret.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/metrics"),
            branch => branch.Use(async (ctx, next) =>
            {
                var opts = ctx.RequestServices.GetRequiredService<IOptions<MetricsOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.BearerToken))
                {
                    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
                    if (authHeader != $"Bearer {opts.BearerToken}")
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }
                }
                await next(ctx);
            }));

        app.MapMetrics("/metrics");

        return app;
    }
}
