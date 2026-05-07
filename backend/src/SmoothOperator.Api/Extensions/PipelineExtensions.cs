using Prometheus;
using Serilog;
using SmoothOperator.Api.Middleware;

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
        app.MapMetrics("/metrics");

        return app;
    }
}
