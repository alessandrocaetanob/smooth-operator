using System;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SmoothOperator.Api.Extensions;

public static class AuthCookieExtensions
{
    public const string CookieName = "access_token";

    public static void SetAuthCookie(this HttpResponse response, string token)
    {
        var expiry = GetTokenExpiry(token);
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/",
            Expires = expiry,
        });
    }

    public static void ClearAuthCookie(this HttpResponse response)
    {
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/",
        });
    }

    private static DateTimeOffset? GetTokenExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expUnix))
                return DateTimeOffset.FromUnixTimeSeconds(expUnix);
        }
        catch
        {
            // If exp cannot be decoded, let the cookie expire at session end
        }
        return null;
    }
}
