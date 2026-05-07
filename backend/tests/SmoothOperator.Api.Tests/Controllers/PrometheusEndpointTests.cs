using System.Net;
using Microsoft.AspNetCore.Hosting;
using SmoothOperator.Api.Tests.Infrastructure;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

/// <summary>
/// Integration tests for the Prometheus /metrics scrape endpoint bearer-token guard.
/// </summary>
public class PrometheusEndpointTests
{
    private const string DevToken = "test-metrics-token";

    private static TestWebApplicationFactory CreateFactoryWithToken() =>
        new(overrideConfig: new Dictionary<string, string?>
        {
            ["Metrics:BearerToken"] = DevToken
        });

    [Fact]
    public async Task Metrics_WithoutTokenConfigured_IsPubliclyAccessible()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_WithTokenConfigured_NoAuthHeader_Returns401()
    {
        await using var factory = CreateFactoryWithToken();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_WithTokenConfigured_WrongToken_Returns401()
    {
        await using var factory = CreateFactoryWithToken();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer wrong-token");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_WithTokenConfigured_CorrectToken_ReturnsSuccess()
    {
        await using var factory = CreateFactoryWithToken();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {DevToken}");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_WithTokenConfigured_LowercaseBearerScheme_ReturnsSuccess()
    {
        await using var factory = CreateFactoryWithToken();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"bearer {DevToken}");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_WithTokenConfigured_ExtraWhitespaceAroundToken_ReturnsSuccess()
    {
        await using var factory = CreateFactoryWithToken();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer   {DevToken}   ");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_WithTokenConfigured_MalformedAuthScheme_Returns401()
    {
        await using var factory = CreateFactoryWithToken();
        var client = factory.CreateClient();
        // Basic auth scheme should be rejected
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {DevToken}");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Metrics_InProductionWithoutToken_ThrowsConfigurationError()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
            _ = factory.CreateClient();
        });
    }
}
