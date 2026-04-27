using System.Net;
using System.Net.Http.Json;
using Backend.Tests.Infrastructure;
using Xunit;

namespace Backend.Tests.Controllers;

/// <summary>
/// Integration tests that verify the "auth" rate-limiting policy applied to
/// <c>AuthController</c>. Each test uses its own <see cref="TestWebApplicationFactory"/>
/// so the in-memory rate-limiter state is fully isolated between test runs.
/// </summary>
public class AuthRateLimitingTests
{
    /// <summary>
    /// Sending more than 5 requests within the same fixed window from the same
    /// IP (or same fallback partition key) should yield HTTP 429 on the 6th call.
    /// </summary>
    [Fact]
    public async Task Login_Returns429_AfterFiveRequestsInWindow()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var body = new { Email = "noone@example.com", Password = "wrong" };

        // First 5 requests must not be rate-limited (they return 401 for bad creds).
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", body);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // The 6th request within the same window must be throttled.
        var throttled = await client.PostAsJsonAsync("/api/auth/login", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    /// <summary>
    /// Verifies that the same rate-limiting policy protects the forgot-password
    /// endpoint, which is another sensitive auth endpoint.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_Returns429_AfterFiveRequestsInWindow()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var body = new { Email = "noone@example.com" };

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/forgot-password", body);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/auth/forgot-password", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
