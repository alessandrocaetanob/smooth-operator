namespace SmoothOperator.Api.Middleware
{
    /// <summary>
    /// Applies OWASP-recommended HTTP security headers to every response.
    /// HSTS is applied only in non-Development environments to avoid breaking
    /// HTTP-only local development.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _env;

        public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var headers = ctx.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), usb=()";
            // Allow WebSocket connections (ws:/wss:) required by Guacamole.
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self' ws: wss:; " +
                "frame-ancestors 'none'";

            if (!_env.IsDevelopment())
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            await _next(ctx);
        }
    }
}
