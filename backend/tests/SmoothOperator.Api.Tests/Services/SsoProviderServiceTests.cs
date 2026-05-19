using SmoothOperator.Application.Options;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SmoothOperator.Api.Tests.Services;

public class SsoProviderServiceTests
{
    private const string TestKey = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EncryptionService NewEncryption() =>
        new EncryptionService(Options.Create(new EncryptionOptions { Key = TestKey }));

    [Fact]
    public async Task GetProviderAsync_ReturnsNull_WhenNoneConfigured()
    {
        await using var ctx = NewContext();
        var svc = new SsoProviderService(ctx, NewEncryption());

        Assert.Null(await svc.GetProviderAsync());
        Assert.Null(await svc.GetActiveProviderAsync());
        Assert.Null(await svc.GetDecryptedOidcAsync());
        Assert.Null(await svc.GetDecryptedSamlAsync());
    }

    [Fact]
    public async Task UpsertOidcAsync_CreatesProviderEncryptedAndDisabled()
    {
        await using var ctx = NewContext();
        var enc = NewEncryption();
        var svc = new SsoProviderService(ctx, enc);

        var saved = await svc.UpsertOidcAsync("Corporate SSO", new OidcConfig
        {
            Authority = "https://idp.example.com",
            ClientId = "client-1",
            ClientSecret = "super-secret",
            Scopes = "openid profile email",
            SubjectClaim = "sub",
            EmailClaim = "email",
            NameClaim = "name"
        });

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.False(saved.IsEnabled);
        Assert.Equal(SsoProviderType.Oidc, saved.Type);

        // Stored payload must not contain the plaintext secret.
        var row = await ctx.SsoProviders.AsNoTracking().FirstAsync();
        Assert.DoesNotContain("super-secret", row.EncryptedConfigJson);

        var dec = await svc.GetDecryptedOidcAsync();
        Assert.NotNull(dec);
        Assert.Equal("super-secret", dec!.ClientSecret);
        Assert.Equal("https://idp.example.com", dec.Authority);

        // Asking for SAML on an OIDC provider returns null.
        Assert.Null(await svc.GetDecryptedSamlAsync());
    }

    [Fact]
    public async Task UpsertOidcAsync_PreservesIdAndEnabledFlag_OnEdit()
    {
        await using var ctx = NewContext();
        var svc = new SsoProviderService(ctx, NewEncryption());

        var first = await svc.UpsertOidcAsync("v1", new OidcConfig
        {
            Authority = "https://a.example.com",
            ClientId = "c",
            ClientSecret = "s"
        });
        await svc.SetEnabledAsync(true);

        var second = await svc.UpsertOidcAsync("v2", new OidcConfig
        {
            Authority = "https://b.example.com",
            ClientId = "c",
            ClientSecret = "s2"
        });

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("v2", second.Name);
        var dec = await svc.GetDecryptedOidcAsync();
        Assert.Equal("https://b.example.com", dec!.Authority);
        Assert.Equal("s2", dec.ClientSecret);

        var current = await svc.GetProviderAsync();
        Assert.True(current!.IsEnabled);
    }

    [Fact]
    public async Task SwitchingProtocol_UpdatesTypeAndPayload()
    {
        await using var ctx = NewContext();
        var svc = new SsoProviderService(ctx, NewEncryption());

        await svc.UpsertOidcAsync("oidc", new OidcConfig
        {
            Authority = "https://a.example.com",
            ClientId = "c",
            ClientSecret = "s"
        });

        var saml = await svc.UpsertSamlAsync("saml", new SamlConfig
        {
            SpEntityId = "sp",
            IdpEntityId = "idp",
            IdpSsoUrl = "https://idp/sso",
            IdpCertificate = "cert",
            SpCertificate = "spcert",
            SpPrivateKey = "key"
        });

        Assert.Equal(SsoProviderType.Saml, saml.Type);
        Assert.Null(await svc.GetDecryptedOidcAsync());
        var dec = await svc.GetDecryptedSamlAsync();
        Assert.Equal("idp", dec!.IdpEntityId);
        Assert.Equal(1, await ctx.SsoProviders.CountAsync());
    }

    [Fact]
    public async Task SetEnabledAsync_TogglesFlag_AndReturnsNull_WhenNoProvider()
    {
        await using var ctx = NewContext();
        var svc = new SsoProviderService(ctx, NewEncryption());

        Assert.Null(await svc.SetEnabledAsync(true));

        await svc.UpsertOidcAsync("n", new OidcConfig { Authority = "https://a", ClientId = "c", ClientSecret = "s" });
        Assert.False((await svc.GetProviderAsync())!.IsEnabled);

        var enabled = await svc.SetEnabledAsync(true);
        Assert.True(enabled!.IsEnabled);
        Assert.NotNull(await svc.GetActiveProviderAsync());

        var disabled = await svc.SetEnabledAsync(false);
        Assert.False(disabled!.IsEnabled);
        Assert.Null(await svc.GetActiveProviderAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesProvider_ReturnsFalseWhenAbsent()
    {
        await using var ctx = NewContext();
        var svc = new SsoProviderService(ctx, NewEncryption());

        Assert.False(await svc.DeleteAsync());

        await svc.UpsertOidcAsync("n", new OidcConfig { Authority = "https://a", ClientId = "c", ClientSecret = "s" });
        Assert.True(await svc.DeleteAsync());
        Assert.Equal(0, await ctx.SsoProviders.CountAsync());
        Assert.False(await svc.DeleteAsync());
    }
}
