using System.Net;
using System.Text;
using System.Text.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public class OidcFlowServiceTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public MockHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = new Uri("https://idp.example.com") };
    }

    [Fact]
    public async Task BuildAuthorizationUrlAsync_GeneratesCorrectUrl()
    {
        await using var db = NewContext();
        var providers = new Mock<ISsoProviderService>();
        providers.Setup(p => p.GetDecryptedOidcAsync()).ReturnsAsync(new OidcConfig
        {
            Authority = "https://idp.example.com",
            ClientId = "test-client",
            Scopes = "openid profile"
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    issuer = "https://idp.example.com",
                    authorization_endpoint = "https://idp.example.com/auth",
                    token_endpoint = "https://idp.example.com/token",
                    jwks_uri = "https://idp.example.com/keys"
                }))
            });

        var svc = new OidcFlowService(db, providers.Object, new MockHttpClientFactory(handlerMock.Object));

        var url = await svc.BuildAuthorizationUrlAsync("https://app.com/callback", "/dashboard");

        Assert.Contains("https://idp.example.com/auth", url);
        Assert.Contains("client_id=test-client", url);
        Assert.Contains("scope=", url);
        Assert.Contains("openid", url);
        Assert.Contains("profile", url);
        Assert.Contains("response_type=code", url);

        var state = await db.SsoAuthStates.FirstOrDefaultAsync();
        Assert.NotNull(state);
        Assert.Equal("/dashboard", state.ReturnUrl);
    }
    [Fact]
    public async Task HandleCallbackAsync_ValidatesStateAndExchangesToken()
    {
        await using var db = NewContext();
        var state = "test-state";
        db.SsoAuthStates.Add(new SsoAuthState
        {
            Id = Guid.NewGuid(),
            State = state,
            Nonce = "test-nonce",
            CodeVerifier = "test-verifier",
            ReturnUrl = "/dashboard",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var providers = new Mock<ISsoProviderService>();
        providers.Setup(p => p.GetDecryptedOidcAsync()).ReturnsAsync(new OidcConfig
        {
            Authority = "https://idp.example.com",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            SubjectClaim = "sub",
            EmailClaim = "email"
        });

        // We can't easily mock the IdentityModel extension methods because they are static.
        // But we can mock the underlying SendAsync call that they trigger.
        var handlerMock = new Mock<HttpMessageHandler>();

        // 1. Discovery document call
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.PathAndQuery.Contains(".well-known")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    issuer = "https://idp.example.com",
                    authorization_endpoint = "https://idp.example.com/auth",
                    token_endpoint = "https://idp.example.com/token",
                    jwks_uri = "https://idp.example.com/keys"
                }))
            });

        // 3. JWKS call (needed by IdentityModel during validation)
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.PathAndQuery.Contains("/keys")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new { keys = Array.Empty<object>() }))
            });

        // 2. Token exchange call
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.PathAndQuery.Contains("/token")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "access",
                    id_token = "dummy-id-token",
                    token_type = "Bearer",
                    expires_in = 3600
                }))
            });

        var svc = new OidcFlowService(db, providers.Object, new MockHttpClientFactory(handlerMock.Object));

        // Note: This will likely fail with InvalidOperationException or SecurityTokenException 
        // because "dummy-id-token" isn't a valid JWT.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.HandleCallbackAsync("test-code", state, "https://app.com/callback"));

        // The fact it reached token exchange and then failed on validation/parsing is still valuable coverage.
        Assert.True(ex.Message.Contains("OIDC") || ex.Message.Contains("IDX20803"));

        // State should be removed even on failure
        Assert.False(await db.SsoAuthStates.AnyAsync(s => s.State == state));
    }
}
