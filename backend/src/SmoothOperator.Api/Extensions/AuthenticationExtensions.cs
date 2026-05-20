using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Extensions;

public static class AuthenticationExtensions
{
    // Build TokenValidationParameters directly from configuration so AddJwtBearer
    // doesn't depend on a resolved IOptions<T> (which isn't available yet during host build).
    // Options validation will still catch misconfiguration at startup via ValidateOnStart().
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtCfg = configuration.GetSection("Jwt");
        var jwtKey = jwtCfg["Key"] ?? throw new InvalidOperationException(
            "Jwt:Key is not configured. Set the Jwt__Key environment variable.");

        services.AddAuthentication(options =>
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
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (string.IsNullOrEmpty(ctx.Token))
                        ctx.Token = ctx.Request.Cookies[AuthCookieExtensions.CookieName];
                    return Task.CompletedTask;
                },
                // Reject tokens whose subject no longer exists or has been deactivated.
                // Without this, a deleted/disabled user keeps full access until the JWT
                // naturally expires (default 60 min). Audit logs would also lose the
                // attribution (UserId would be null via AuditService's existence check).
                OnTokenValidated = async ctx =>
                {
                    var idClaim = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!Guid.TryParse(idClaim, out var userId))
                    {
                        ctx.Fail("Token subject is not a valid user identifier.");
                        return;
                    }

                    var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var isActive = await db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => (bool?)u.IsActive)
                        .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

                    if (isActive is null)
                    {
                        ctx.Fail("Token subject does not exist.");
                        return;
                    }
                    if (isActive == false)
                    {
                        ctx.Fail("Token subject is deactivated.");
                    }
                },
            };
        });

        return services;
    }
}
