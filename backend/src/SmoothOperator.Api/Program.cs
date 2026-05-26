using SmoothOperator.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Docker Secrets ──────────────────────────────────────────────────────────
// When running in Docker with the secrets: block, secrets are mounted as files
// at /run/secrets/<KEY_NAME>. Key names use __ as the section separator so that
// e.g. /run/secrets/Jwt__Key maps to the Jwt:Key config key.
// File-based secrets take precedence over appsettings.json values.
builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);

// Replace default logging with Serilog — reads config from "Serilog" section.
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddApplicationOptions();
builder.Services.AddApplicationCoreServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationRateLimiting(builder.Configuration);
builder.Services.AddApplicationForwardedHeaders();
builder.Services.AddApplicationHealthChecks(builder.Configuration);
builder.Services.AddApplicationDataProtection(builder.Configuration);
builder.Services.AddApplicationCors(builder.Configuration, builder.Environment);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Accept and emit enum members as strings (e.g. "Inherit", not 0). Lets the
        // Angular frontend use semantic names in request payloads + responses.
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddApplicationResponseCompression();
builder.Services.AddApplicationOutputCache(builder.Configuration);
builder.Services.AddApplicationLayer();
builder.AddSmoothOperatorOpenTelemetry();

var app = builder.Build();

// Apply any pending EF Core migrations on startup.
await app.ApplyPendingMigrationsAsync();

app.UseApplicationPipeline();

await app.RunAsync();


// Expose the implicit Program class for WebApplicationFactory<Program> in tests.
public partial class Program
{
    protected Program() { }
}
