using Backend.Data;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<GuacamoleProxyService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInviteService, InviteService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddHostedService<AuditRetentionService>();

// Local JWT (HS256) is the default authentication scheme so the app works without
// any external identity provider. Entra ID (or any other OIDC provider) is opt-in
// and only registered when the relevant configuration is present.
const string LocalScheme = JwtBearerDefaults.AuthenticationScheme;
const string EntraScheme = "EntraId";

var tokenService = new TokenService(builder.Configuration);
builder.Services.AddSingleton<ITokenService>(tokenService);

var entraSection = builder.Configuration.GetSection("AzureAd");
var entraEnabled = !string.IsNullOrWhiteSpace(entraSection["ClientId"])
                   && !string.IsNullOrWhiteSpace(entraSection["TenantId"]);

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = LocalScheme;
    options.DefaultChallengeScheme = LocalScheme;
});

authBuilder.AddJwtBearer(LocalScheme, options =>
{
    options.TokenValidationParameters = tokenService.BuildValidationParameters();

    // When Entra is enabled, hand off tokens whose issuer matches Entra to the Entra handler.
    if (entraEnabled)
    {
        options.ForwardDefaultSelector = ctx =>
        {
            var auth = ctx.Request.Headers["Authorization"].ToString();
            const string bearer = "Bearer ";
            if (!auth.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return LocalScheme;
            }

            var token = auth.Substring(bearer.Length).Trim();
            try
            {
                var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
                var iss = jwt.Issuer ?? string.Empty;
                if (iss.Contains("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase)
                    || iss.Contains("sts.windows.net", StringComparison.OrdinalIgnoreCase))
                {
                    return EntraScheme;
                }
            }
            catch
            {
                // fall through – local handler will reject malformed tokens
            }
            return LocalScheme;
        };
    }
});

if (entraEnabled)
{
    authBuilder.AddMicrosoftIdentityWebApi(entraSection, jwtBearerScheme: EntraScheme);
}

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

builder.Services.AddControllers();

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

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();


// Expose the implicit Program class for WebApplicationFactory<Program> in tests.
public partial class Program { }

