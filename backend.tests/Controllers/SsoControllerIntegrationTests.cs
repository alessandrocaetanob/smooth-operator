using System.Net;
using System.Net.Http.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Services.Sso;
using Backend.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests.Controllers;

public class SsoControllerIntegrationTests
{
    [Fact]
    public async Task GetProvider_ReturnsDisabled_WhenNoneConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/auth/sso/provider");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<SsoStatusDto>();
        Assert.NotNull(dto);
        Assert.False(dto!.Enabled);
        Assert.Null(dto.Type);
        Assert.Null(dto.Name);
    }

    [Fact]
    public async Task GetProvider_ReturnsDisabled_WhenConfiguredButNotEnabled()
    {
        await using var factory = new TestWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
            await providers.UpsertOidcAsync("Corp", new OidcConfig
            {
                Authority = "https://idp.example.com",
                ClientId = "c",
                ClientSecret = "s"
            });
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/auth/sso/provider");
        var dto = await res.Content.ReadFromJsonAsync<SsoStatusDto>();
        Assert.False(dto!.Enabled);
    }

    [Fact]
    public async Task GetProvider_ReturnsEnabledShape_WhenActive()
    {
        await using var factory = new TestWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
            await providers.UpsertOidcAsync("Corp SSO", new OidcConfig
            {
                Authority = "https://idp.example.com",
                ClientId = "c",
                ClientSecret = "s"
            });
            await providers.SetEnabledAsync(true);
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/auth/sso/provider");
        var dto = await res.Content.ReadFromJsonAsync<SsoStatusDto>();
        Assert.True(dto!.Enabled);
        Assert.Equal("Oidc", dto.Type);
        Assert.Equal("Corp SSO", dto.Name);
    }

    [Fact]
    public async Task Initiate_ReturnsNotFound_WhenSsoNotConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/auth/sso/initiate");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Metadata_ReturnsNotFound_WhenSamlNotConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/auth/sso/metadata");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Metadata_ReturnsNotFound_WhenOidcConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
            await providers.UpsertOidcAsync("Corp", new OidcConfig
            {
                Authority = "https://idp.example.com",
                ClientId = "c",
                ClientSecret = "s"
            });
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/auth/sso/metadata");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
