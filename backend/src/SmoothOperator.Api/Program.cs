using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Api.Middleware;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using SmoothOperator.Application.Options;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Application.Behaviors;
using SmoothOperator.Application.Features.Auth.Commands;
using FluentValidation;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using IPNetwork = System.Net.IPNetwork;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using StackExchange.Redis;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Replace default logging with Serilog — reads config from "Serilog" section.
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// ─── Options Registration ────────────────────────────────────────────────────
// All options classes are registered with DataAnnotations validation and
// ValidateOnStart() so misconfiguration is caught at startup, not at first request.

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration("Jwt")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<GuacdOptions>()
    .BindConfiguration("Guacd")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EncryptionOptions>()
    .BindConfiguration("Encryption")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<OtelOptions>()
    .BindConfiguration("Otel")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppUrlsOptions>()
    .BindConfiguration("AppUrls")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RateLimitOptions>()
    .BindConfiguration("RateLimit")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ─── Core Services ───────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis — registered as singleton IConnectionMultiplexer so GuacamoleProxyService
// and other consumers share one multiplexed connection rather than each opening one.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<GuacamoleProxyService>();
builder.Services.AddSingleton<IAppMetrics, AppMetrics>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInviteService, InviteService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddHostedService<AuditRetentionService>();

// SSO services
builder.Services.AddScoped<ISsoProviderService, SsoProviderService>();
builder.Services.AddScoped<IOidcFlowService, OidcFlowService>();
builder.Services.AddScoped<ISamlFlowService, SamlFlowService>();
builder.Services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
builder.Services.AddScoped<ISsoConnectionTester, SsoConnectionTester>();
builder.Services.AddScoped<SsoUrlHelper>();
builder.Services.AddHttpClient();

// ─── Authentication ──────────────────────────────────────────────────────────
// Build TokenValidationParameters directly from configuration so AddJwtBearer
// doesn't depend on a resolved IOptions<T> (which isn't available yet during host build).
// Options validation will still catch misconfiguration at startup via ValidateOnStart().
var jwtCfg = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtCfg["Key"] ?? throw new InvalidOperationException(
    "Jwt:Key is not configured. Set the Jwt__Key environment variable.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtCfg["Issuer"] ?? TokenService.LocalIssuer,
        ValidAudience = jwtCfg["Audience"] ?? TokenService.LocalAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero,
    };
});

// ─── Rate Limiting ───────────────────────────────────────────────────────────
// Read limits directly from config at startup (same values registered in IOptions<RateLimitOptions>).
var rlCfg = builder.Configuration.GetSection("RateLimit");
var globalPermit = rlCfg.GetValue<int>("Global:PermitLimit", 100);
var globalWindow = rlCfg.GetValue<int>("Global:WindowSeconds", 60);
var authPermit = rlCfg.GetValue<int>("Auth:PermitLimit", 5);
var authWindow = rlCfg.GetValue<int>("Auth:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
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

// Trust the X-Forwarded-For / X-Forwarded-Proto headers sent by the nginx reverse-proxy
// container so rate-limiting partitions by the real client IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});

// ─── Health Checks ───────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

builder.Services.AddControllers();

// ─── Application layer: MediatR, FluentValidation, Mapster ──────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly);
    cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(LoginCommandHandler).Assembly);
builder.Services.AddScoped<IMapper, Mapper>();
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// ─── OpenTelemetry ───────────────────────────────────────────────────────────
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

// ─── Build ───────────────────────────────────────────────────────────────────
var app = builder.Build();

// Apply any pending EF Core migrations on startup.
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

