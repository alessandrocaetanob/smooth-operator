using SmoothOperator.Application.Features.Webhooks;

namespace SmoothOperator.Application.Tests.Features.Webhooks;

public sealed class WebhookEventMatcherTests
{
    [Theory]
    [InlineData("*", "user.login_success")]
    [InlineData("*", "connection.started")]
    [InlineData("user.*", "user.login_success")]
    [InlineData("user.*", "user.created")]
    [InlineData("connection.started", "connection.started")]
    [InlineData("CONNECTION.STARTED", "connection.started")]
    [InlineData("user.*,connection.*", "connection.deleted")]
    [InlineData(" user.* , connection.* ", "user.created")]
    public void Matches_ReturnsTrue_ForMatchingPattern(string patterns, string action)
    {
        Assert.True(WebhookEventMatcher.Matches(patterns, action));
    }

    [Theory]
    [InlineData("user.*", "connection.started")]
    [InlineData("user.*", "userspace.created")] // prefix keeps the dot
    [InlineData("connection.started", "connection.stopped")]
    [InlineData("user.*,credential.*", "connection.started")]
    public void Matches_ReturnsFalse_ForNonMatchingPattern(string patterns, string action)
    {
        Assert.False(WebhookEventMatcher.Matches(patterns, action));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_ReturnsFalse_ForEmptyPatterns(string? patterns)
    {
        Assert.False(WebhookEventMatcher.Matches(patterns, "user.login_success"));
    }

    [Fact]
    public void Matches_ReturnsFalse_ForEmptyAction()
    {
        Assert.False(WebhookEventMatcher.Matches("*", ""));
    }
}
