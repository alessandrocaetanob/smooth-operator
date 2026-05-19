using System.Linq;
using Microsoft.AspNetCore.Http;
using SmoothOperator.Api.Extensions;
using Xunit;

namespace SmoothOperator.Api.Tests.Extensions;

public class AuthCookieExtensionsTests
{
    // Minimal unsigned JWT with payload {"exp": 9999999999} so the cookie sets an Expires.
    private const string SampleToken = "header.eyJleHAiOjk5OTk5OTk5OTl9.sig";

    [Fact]
    public void SetAuthCookie_OverHttps_SetsSecureTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        context.Response.SetAuthCookie(SampleToken);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieExtensions.CookieName, setCookie);
        Assert.Contains("secure", setCookie, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetAuthCookie_OverHttp_OmitsSecureFlag()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 4200);

        context.Response.SetAuthCookie(SampleToken);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieExtensions.CookieName, setCookie);
        Assert.DoesNotContain("secure", setCookie, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAuthCookie_OverHttps_SetsSecureTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        context.Response.ClearAuthCookie();

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieExtensions.CookieName, setCookie);
        Assert.Contains("secure", setCookie, System.StringComparison.OrdinalIgnoreCase);
        // Cookie deletion sets expiry in the past
        Assert.Contains("expires=", setCookie, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAuthCookie_OverHttp_OmitsSecureFlag()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 4200);

        context.Response.ClearAuthCookie();

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieExtensions.CookieName, setCookie);
        Assert.DoesNotContain("secure", setCookie, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetAuthCookie_WithMalformedToken_DoesNotThrow()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        // No exception even though token has no payload
        context.Response.SetAuthCookie("not-a-jwt");

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieExtensions.CookieName, setCookie);
    }
}
