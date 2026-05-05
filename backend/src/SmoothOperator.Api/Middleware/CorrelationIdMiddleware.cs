using Serilog.Context;

namespace SmoothOperator.Api.Middleware
{
    /// <summary>
    /// Reads (or generates) X-Correlation-Id for every inbound request.
    /// Stores it in HttpContext.Items["CorrelationId"] so that AuditService
    /// and log enrichers can pick it up without extra dependencies.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx)
        {
            var correlationId = ctx.Request.Headers[HeaderName].FirstOrDefault()
                                ?? Guid.NewGuid().ToString("N");

            ctx.Items["CorrelationId"] = correlationId;
            ctx.Response.Headers[HeaderName] = correlationId;

            // Push into Serilog LogContext so all log lines for this request carry it.
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(ctx);
            }
        }
    }
}
