using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.Options;
using SmoothOperator.Infrastructure.Services.Sso;

namespace SmoothOperator.Api.Tests.Services;

public class SsoUrlHelperTests
{
    private static SsoUrlHelper MakeHelper(string app, string frontend) =>
        new(Options.Create(new AppUrlsOptions { App = app, Frontend = frontend }));

    [Fact]
    public void CallbackUrl_UsesApiBase_WhenAppUrlConfigured()
    {
        var helper = MakeHelper("https://api.example.com", "https://app.example.com");
        var req = new DefaultHttpContext().Request;

        helper.CallbackUrl(req).Should().Be("https://api.example.com/api/auth/sso/callback");
    }

    [Fact]
    public void FinalizeUrl_UsesFrontendBase_WhenFrontendUrlConfigured()
    {
        var helper = MakeHelper("https://api.example.com", "https://app.example.com");
        var req = new DefaultHttpContext().Request;

        helper.FinalizeUrl(req, "/vault").Should().Be("https://app.example.com/auth/sso/finalize#returnUrl=%2Fvault");
    }

    [Fact]
    public void CallbackAndFinalize_UseDifferentBases_WhenBothConfigured()
    {
        var helper = MakeHelper("https://api.example.com", "https://app.example.com");
        var req = new DefaultHttpContext().Request;

        helper.CallbackUrl(req).Should().StartWith("https://api.example.com");
        helper.FinalizeUrl(req, "/").Should().StartWith("https://app.example.com");
    }

    [Fact]
    public void CallbackUrl_FallsBackToRequest_WhenNoUrlsConfigured()
    {
        var helper = MakeHelper("", "");
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("fallback.example.com");

        helper.CallbackUrl(ctx.Request).Should().Be("https://fallback.example.com/api/auth/sso/callback");
    }

    [Fact]
    public void FinalizeUrl_FallsBackToRequest_WhenNoUrlsConfigured()
    {
        var helper = MakeHelper("", "");
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("fallback.example.com");

        helper.FinalizeUrl(ctx.Request, "/vault").Should().Be("https://fallback.example.com/auth/sso/finalize#returnUrl=%2Fvault");
    }


    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    [InlineData("/vault", "/vault")]
    [InlineData("/vault/connections/123", "/vault/connections/123")]
    [InlineData("/", "/")]
    public void SanitizeReturnUrl_AcceptsSafeRelativePaths(string? input, string expected)
    {
        Assert.Equal(expected, SsoUrlHelper.SanitizeReturnUrl(input));
    }

    [Theory]
    [InlineData("https://evil.com/")]
    [InlineData("//evil.com/path")]
    [InlineData("/path\\with\\backslash")]
    [InlineData("javascript:alert(1)")]
    [InlineData("vault")]
    [InlineData("http://attacker/")]
    public void SanitizeReturnUrl_RejectsUnsafeInputs(string input)
    {
        Assert.Equal("/", SsoUrlHelper.SanitizeReturnUrl(input));
    }

    [Fact]
    public void SanitizeReturnUrl_RejectsTooLongInput()
    {
        var input = "/" + new string('a', 1024);
        Assert.Equal("/", SsoUrlHelper.SanitizeReturnUrl(input));
    }
}
