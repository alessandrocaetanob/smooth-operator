using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

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
        Assert.False(dto.Enabled);
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
    [Fact]
    public async Task Initiate_RedirectsToOidc_WhenOidcConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        var oidcMock = new Mock<IOidcFlowService>();
        oidcMock.Setup(s => s.BuildAuthorizationUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://idp.com/auth?test=1");

        using (var scope = factory.Services.CreateScope())
        {
            var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
            await providers.UpsertOidcAsync("Corp", new OidcConfig { Authority = "https://idp.com", ClientId = "c" });
            await providers.SetEnabledAsync(true);
        }

        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(oidcMock.Object);
            });
        }).CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/auth/sso/initiate?returnUrl=/dash");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("https://idp.com/auth?test=1", res.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Initiate_RedirectsToSaml_WhenSamlConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        var samlMock = new Mock<ISamlFlowService>();
        samlMock.Setup(s => s.BuildAuthnRequestUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://saml-idp.com/sso");

        using (var scope = factory.Services.CreateScope())
        {
            var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
            await providers.UpsertSamlAsync("Corp SAML", new SamlConfig { IdpSsoUrl = "https://idp.com/sso" });
            await providers.SetEnabledAsync(true);
        }

        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(samlMock.Object);
            });
        }).CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/auth/sso/initiate");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("https://saml-idp.com/sso", res.Headers.Location?.ToString());
    }

    [Fact]
    public async Task OidcCallback_RedirectsToFinalize_OnSuccess()
    {
        await using var factory = new TestWebApplicationFactory();
        var oidcMock = new Mock<IOidcFlowService>();
        oidcMock.Setup(s => s.HandleCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((new OidcCallbackResult("ext-1", "user@test.com", "Test User"), "/dash"));

        var provisioningMock = new Mock<ISsoUserProvisioningService>();
        provisioningMock.Setup(s => s.ProvisionOrLinkAsync(It.IsAny<SsoProviderType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new SsoProvisioningResult(new User { Id = Guid.NewGuid(), Email = "user@test.com" }, "test-token", false, false));

        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(oidcMock.Object);
                services.AddSingleton(provisioningMock.Object);
            });
        }).CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/auth/sso/callback?code=123&state=abc");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var location = res.Headers.Location?.ToString();
        Assert.DoesNotContain("test-token", location);
        Assert.Contains("returnUrl=%2Fdash", location);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, c => c.StartsWith("access_token=test-token"));
    }

    [Fact]
    public async Task OidcCallback_RedirectsToError_WhenIdpReturnsError()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/auth/sso/callback?error=access_denied");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("error=access_denied", res.Headers.Location?.ToString());
    }

    [Fact]
    public async Task OidcCallback_RedirectsToError_OnUnauthorizedAccess()
    {
        await using var factory = new TestWebApplicationFactory();
        var oidcMock = new Mock<IOidcFlowService>();
        oidcMock.Setup(s => s.HandleCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("User is disabled"));

        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(oidcMock.Object);
            });
        }).CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/auth/sso/callback?code=123&state=abc");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("error=unauthorized", res.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Metadata_ReturnsXml_WhenSamlConfigured()
    {
        await using var factory = new TestWebApplicationFactory();
        var samlMock = new Mock<ISamlFlowService>();
        samlMock.Setup(s => s.GetSpMetadataAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("<EntityDescriptor />");

        using (var scope = factory.Services.CreateScope())
        {
            var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
            await providers.UpsertSamlAsync("Corp", new SamlConfig { IdpSsoUrl = "https://idp.com/sso" });
        }

        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(samlMock.Object);
            });
        }).CreateClient();

        var res = await client.GetAsync("/api/auth/sso/metadata");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/samlmetadata+xml", res.Content.Headers.ContentType?.MediaType);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Equal("<EntityDescriptor />", content);
    }
}
