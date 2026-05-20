using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Api.Authentication;

/// <summary>
/// Validates <c>Authorization: Bearer sop_&lt;lookup&gt;_&lt;secret&gt;</c> headers against
/// hashed entries in <see cref="AppDbContext.ApiTokens"/>. On success the request
/// runs with the owning user's identity (same claim shape as a JWT-authenticated request).
/// </summary>
public sealed class ApiTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiToken";
    public const string TokenPrefix = "sop_";
    private const string BearerPrefix = "Bearer ";

    public ApiTokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            return AuthenticateResult.NoResult();

        var header = headerValues.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var raw = header[BearerPrefix.Length..].Trim();
        if (!raw.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        // Token format: sop_<lookup>_<secret>. We need lookup = "sop_<lookup>".
        var secondUnderscore = raw.IndexOf('_', TokenPrefix.Length);
        if (secondUnderscore <= TokenPrefix.Length || secondUnderscore == raw.Length - 1)
            return AuthenticateResult.Fail("Malformed API token.");

        var lookup = raw[..secondUnderscore];
        var db = Context.RequestServices.GetRequiredService<AppDbContext>();

        var token = await db.ApiTokens
            .Include(t => t.User).ThenInclude(u => u.Roles)
            .FirstOrDefaultAsync(t => t.TokenLookup == lookup, Context.RequestAborted);

        if (token is null) return AuthenticateResult.Fail("API token not recognized.");
        if (token.RevokedAt is not null) return AuthenticateResult.Fail("API token revoked.");
        if (token.ExpiresAt is { } exp && exp <= DateTime.UtcNow) return AuthenticateResult.Fail("API token expired.");
        if (!token.User.IsActive) return AuthenticateResult.Fail("Token owner is deactivated.");
        if (!BCrypt.Net.BCrypt.Verify(raw, token.TokenHash)) return AuthenticateResult.Fail("API token signature mismatch.");

        // Update LastUsedAt without blocking the request. Fire-and-forget on a fresh
        // DbContext scope because the request-scoped one will be disposed before this completes.
        var serviceProvider = Context.RequestServices.GetRequiredService<IServiceProvider>();
        var tokenId = token.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var bgDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await bgDb.ApiTokens
                    .Where(t => t.Id == tokenId)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, _ => DateTime.UtcNow));
            }
            catch
            {
                // best-effort; never fail the request because of a LastUsedAt bump
            }
        });

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, token.User.Id.ToString()),
            new(ClaimTypes.NameIdentifier, token.User.Id.ToString()),
            new(ClaimTypes.Email, token.User.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(token.User.Name) ? token.User.Email : token.User.Name),
            // Marker claim so PAT-authenticated requests can be detected by callers (e.g. blocking
            // PAT-from-PAT creation). Value is the scheme name; presence is the signal.
            new("amr", SchemeName),
        };
        foreach (var role in token.User.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role.Name));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
