using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
            };
        });

        return services;
    }
}
