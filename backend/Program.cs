using Backend.Data;
using Backend.Middleware;
using Backend.Services;
using Backend.Services.Sso;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using IPNetwork = System.Net.IPNetwork;
using Serilog;
using Serilog.Formatting.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Replace default logging with Serilog — reads config from "Serilog" section.
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<GuacamoleProxyService>();
builder.Services.AddSingleton<IAppMetrics, AppMetrics>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInviteService, InviteService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddHostedService<AuditRetentionService>();

// SSO services. Single configured provider (OIDC or SAML 2.0) — the app issues
// its own short-lived HS256 JWT after the IdP flow, so all authenticated requests
// always go through the local JwtBearer handler regardless of how the user logged in.
builder.Services.AddScoped<ISsoProviderService, SsoProviderService>();
builder.Services.AddScoped<IOidcFlowService, OidcFlowService>();
builder.Services.AddScoped<ISamlFlowService, SamlFlowService>();
builder.Services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
builder.Services.AddScoped<ISsoConnectionTester, SsoConnectionTester>();
builder.Services.AddScoped<SsoUrlHelper>();
builder.Services.AddHttpClient();

// Local JWT (HS256) is the only token format we accept on the wire — even SSO
// users carry a JWT minted by TokenService after the IdP flow completes.
var tokenService = new TokenService(builder.Configuration);
builder.Services.AddSingleton<ITokenService>(tokenService);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = tokenService.BuildValidationParameters();
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Trust the X-Forwarded-For / X-Forwarded-Proto headers sent by the nginx reverse-proxy
// container so that the "auth" rate-limiting policy partitions by the real client IP
// rather than the nginx container address.  Restrict trust to RFC-1918 private ranges
// (the docker bridge networks live inside these ranges).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear the default localhost-only allow-list and replace it with all RFC-1918
    // private address ranges, which covers any docker bridge subnet.
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

builder.Services.AddControllers();

// OpenTelemetry distributed tracing — exports to Tempo via OTLP.
var otelEndpoint = builder.Configuration["Otel:Endpoint"];
if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            builder.Configuration["Otel:ServiceName"] ?? "smooth-operator-backend"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));
}

var app = builder.Build();

// Apply any pending EF Core migrations on startup so the schema is always in
// sync with the deployed binary. Wrapped in a scope because AppDbContext is scoped.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    try
    {
        if (db.Database.IsRelational())
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("Database is up to date; no migrations to apply.");
            }
        }
        else
        {
            // InMemory and other non-relational providers (used by tests) do not
            // support migrations. Just ensure the in-memory store is created.
            await db.Database.EnsureCreatedAsync();
        }

        await RoleSeeder.SeedDefaultsAsync(db, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations on startup.");
        throw;
    }
}

app.UseWebSockets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// UseForwardedHeaders must come before UseHttpsRedirection so that the
// X-Forwarded-Proto header is visible when the HTTPS redirect decision is made.
app.UseForwardedHeaders();

// Security + observability middleware — ordered before routing.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("CorrelationId", ctx.Items["CorrelationId"]);
        diag.Set("UserId", ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    };
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseHttpMetrics();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics("/metrics");

app.Run();


// Expose the implicit Program class for WebApplicationFactory<Program> in tests.
public partial class Program { }

