using System.Net;
using System.Net.Http.Json;
using Backend.Tests.Infrastructure;
using Xunit;

namespace Backend.Tests.Controllers;

/// <summary>
/// Integration tests that verify the "auth" rate-limiting policy applied to the
/// sensitive actions on <c>AuthController</c> (Login, ForgotPassword, Register).
/// Each test uses its own <see cref="TestWebApplicationFactory"/>
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

    /// <summary>
    /// Verifies that the setup endpoint is protected by the "auth" rate-limiting
    /// policy. The factory is created without a phantom owner so the users table
    /// starts empty (a precondition for setup to succeed at all).
    /// </summary>
    [Fact]
    public async Task Setup_Returns429_AfterFiveRequestsInWindow()
    {
        // seedPhantomOwner:false → Users table is empty so setup can proceed.
        await using var factory = new TestWebApplicationFactory(seedPhantomOwner: false);
        var client = factory.CreateClient();

        var body = new { Email = "admin@example.com", Name = "Admin User", Password = "SuperSecret123!" };

        // First 5 requests must not be rate-limited.
        // The very first call succeeds (200); subsequent ones return 409 (already set up).
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/setup", body);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // The 6th request within the same window must be throttled.
        var throttled = await client.PostAsJsonAsync("/api/auth/setup", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    /// <summary>
    /// Verifies that the redeem-invite endpoint is protected by the "auth"
    /// rate-limiting policy. A non-existent token is used so the first five
    /// requests return 404 (invalid/unknown token) without side-effects; the
    /// sixth must be throttled to 429.
    /// </summary>
    [Fact]
    public async Task RedeemInvite_Returns429_AfterFiveRequestsInWindow()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var body = new { Password = "SuperSecret123!" };

        // Use a deliberately nonexistent token — the endpoint returns 404 but
        // each request still counts against the rate-limit partition.
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/invites/nonexistent-token/redeem", body);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // The 6th request within the same window must be throttled.
        var throttled = await client.PostAsJsonAsync("/api/invites/nonexistent-token/redeem", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
