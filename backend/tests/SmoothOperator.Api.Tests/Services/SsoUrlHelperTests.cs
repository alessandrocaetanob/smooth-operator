using SmoothOperator.Infrastructure.Services.Sso;

namespace SmoothOperator.Api.Tests.Services;

public class SsoUrlHelperTests
{
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
