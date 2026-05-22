using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Api.Authentication;

/// <summary>
/// Validates <c>Authorization: Bearer sop_&lt;lookup&gt;_&lt;secret&gt;</c> headers against
/// SHA-256 hashed entries in <see cref="AppDbContext.ApiTokens"/>. On success the
/// request runs with the owning user's identity (same claim shape as a
/// JWT-authenticated request).
/// </summary>
public sealed class ApiTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiToken";
    public const string TokenPrefix = "sop_";
    private const string BearerPrefix = "Bearer ";

    private readonly IServiceScopeFactory _scopeFactory;

    public ApiTokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceScopeFactory scopeFactory)
        : base(options, logger, encoder)
    {
        _scopeFactory = scopeFactory;
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
        if (ValidateToken(token, raw) is { } failure) return failure;

        // Update LastUsedAt without blocking the request. Resolve IServiceScopeFactory
        // (a singleton) up front — Context.RequestServices would already be disposed
        // by the time this Task.Run callback fires.
        var tokenId = token.Id;
        var scopeFactory = _scopeFactory;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
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

    /// <summary>
    /// Verifies a resolved token's lifecycle state (revocation, expiry, owner
    /// status) and signature. Returns a failure result, or <c>null</c> when the
    /// token is valid. Extracted to keep <see cref="HandleAuthenticateAsync"/>'s
    /// cognitive complexity within limits.
    /// </summary>
    private static AuthenticateResult? ValidateToken(ApiToken token, string rawToken)
    {
        if (token.RevokedAt is not null) return AuthenticateResult.Fail("API token revoked.");
        if (token.ExpiresAt is { } exp && exp <= DateTime.UtcNow) return AuthenticateResult.Fail("API token expired.");
        if (!token.User.IsActive) return AuthenticateResult.Fail("Token owner is deactivated.");
        if (!FixedTimeHashEquals(rawToken, token.TokenHash)) return AuthenticateResult.Fail("API token signature mismatch.");
        return null;
    }

    /// <summary>
    /// Constant-time SHA-256 hash comparison. Both inputs are hashed into fixed-size
    /// 32-byte buffers, so the comparison length is invariant of stored-hash format.
    /// </summary>
    private static bool FixedTimeHashEquals(string plaintext, string base64StoredHash)
    {
        Span<byte> computed = stackalloc byte[32];
        if (!SHA256.TryHashData(Encoding.UTF8.GetBytes(plaintext), computed, out _))
            return false;

        Span<byte> stored = stackalloc byte[32];
        if (!Convert.TryFromBase64String(base64StoredHash, stored, out var storedLen) || storedLen != 32)
            return false;

        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
