using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SmoothOperator.Api.Tests.Controllers;

public class SsoSettingsControllerIntegrationTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var n in roleNames)
            user.Roles.Add(db.Roles.First(r => r.Name == n));
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0) c.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return c;
    }

    private static (TestWebApplicationFactory factory, Guid ownerId, Guid userId) NewFactoryWithUsers()
    {
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var factory = new TestWebApplicationFactory(db =>
        {
            var owner = new User { Id = ownerId, Email = "owner@x", Name = "owner", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, owner, AppRoles.Owner);
            db.Users.Add(owner);
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });
        return (factory, ownerId, userId);
    }

    [Fact]
    public async Task Get_RequiresOwnerOrAdmin()
    {
        var (factory, _, userId) = NewFactoryWithUsers();
        await using var _f = factory;

        var asUser = AsUser(factory, userId, AppRoles.User);
        var res = await asUser.GetAsync("/api/settings/sso");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsEmptyShape_WhenNoneConfigured()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        var res = await asOwner.GetAsync("/api/settings/sso");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<SsoProviderDto>();
        Assert.NotNull(dto);
        Assert.Equal(string.Empty, dto!.Type);
        Assert.False(dto.IsEnabled);
    }

    [Fact]
    public async Task UpsertOidc_PersistsConfig_AndGetReturnsSanitizedView()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        var put = await asOwner.PutAsJsonAsync("/api/settings/sso/oidc", new UpsertOidcRequest
        {
            Name = "Corporate",
            Authority = "https://idp.example.com/",
            ClientId = "client-1",
            ClientSecret = "super-secret",
            Scopes = "openid profile email",
            SubjectClaim = "sub",
            EmailClaim = "email",
            NameClaim = "name"
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var get = await asOwner.GetAsync("/api/settings/sso");
        var dto = await get.Content.ReadFromJsonAsync<SsoProviderDto>();
        Assert.NotNull(dto);
        Assert.Equal("Oidc", dto!.Type);
        Assert.Equal("Corporate", dto.Name);
        Assert.False(dto.IsEnabled);
        Assert.NotNull(dto.Oidc);
        Assert.Equal("https://idp.example.com", dto.Oidc!.Authority); // trailing slash trimmed
        Assert.True(dto.Oidc.HasClientSecret);
        // Secret never returned to the client.
        var raw = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("super-secret", raw);

        // Audit row written.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "sso.config_updated"));
    }

    [Fact]
    public async Task UpsertOidc_WithEmptySecret_OnFreshConfig_Returns400()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        var put = await asOwner.PutAsJsonAsync("/api/settings/sso/oidc", new UpsertOidcRequest
        {
            Name = "Corporate",
            Authority = "https://idp.example.com",
            ClientId = "client-1",
            ClientSecret = null
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task UpsertOidc_WithNullSecret_OnEdit_PreservesExistingSecret()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        await asOwner.PutAsJsonAsync("/api/settings/sso/oidc", new UpsertOidcRequest
        {
            Name = "v1",
            Authority = "https://idp.example.com",
            ClientId = "c",
            ClientSecret = "first-secret"
        });

        // Edit without sending secret.
        var put2 = await asOwner.PutAsJsonAsync("/api/settings/sso/oidc", new UpsertOidcRequest
        {
            Name = "v2",
            Authority = "https://idp.example.com",
            ClientId = "c",
            ClientSecret = null
        });
        Assert.Equal(HttpStatusCode.NoContent, put2.StatusCode);

        using var scope = factory.Services.CreateScope();
        var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
        var dec = await providers.GetDecryptedOidcAsync();
        Assert.Equal("first-secret", dec!.ClientSecret);
    }

    [Fact]
    public async Task Toggle_BeforeUpsert_Returns404_AndAfterUpsert_Enables()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);

        var early = await asOwner.PostAsJsonAsync("/api/settings/sso/toggle", new SetSsoEnabledRequest { Enabled = true });
        Assert.Equal(HttpStatusCode.NotFound, early.StatusCode);

        await asOwner.PutAsJsonAsync("/api/settings/sso/oidc", new UpsertOidcRequest
        {
            Name = "v1",
            Authority = "https://idp.example.com",
            ClientId = "c",
            ClientSecret = "s"
        });
        var ok = await asOwner.PostAsJsonAsync("/api/settings/sso/toggle", new SetSsoEnabledRequest { Enabled = true });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        using var scope = factory.Services.CreateScope();
        var providers = scope.ServiceProvider.GetRequiredService<ISsoProviderService>();
        Assert.True((await providers.GetProviderAsync())!.IsEnabled);
    }

    [Fact]
    public async Task Delete_RemovesProvider_AndReturns404_OnSecondCall()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        await asOwner.PutAsJsonAsync("/api/settings/sso/oidc", new UpsertOidcRequest
        {
            Name = "v1",
            Authority = "https://idp.example.com",
            ClientId = "c",
            ClientSecret = "s"
        });

        var first = await asOwner.DeleteAsync("/api/settings/sso");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await asOwner.DeleteAsync("/api/settings/sso");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task UpsertSaml_PersistsConfig_AndGetReturnsSanitizedView()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        var put = await asOwner.PutAsJsonAsync("/api/settings/sso/saml", new UpsertSamlRequest
        {
            Name = "Corporate SAML",
            SpEntityId = "smooth-operator-sp",
            IdpEntityId = "idp-entity",
            IdpSsoUrl = "https://idp.example.com/sso",
            IdpCertificate = "CERTIFICATE_DATA",
            SpCertificate = "SP_CERTIFICATE_DATA",
            SpPrivateKey = "SUPER_PRIVATE_KEY",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            AttributeEmail = "email",
            AttributeName = "name",
            WantAssertionsSigned = true,
            WantResponseSigned = true
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var get = await asOwner.GetAsync("/api/settings/sso");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<SsoProviderDto>();
        Assert.NotNull(dto);
        Assert.Equal("Saml", dto!.Type);
        Assert.Equal("Corporate SAML", dto.Name);
        Assert.NotNull(dto.Saml);
        Assert.True(dto.Saml!.HasSpPrivateKey);

        var raw = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SUPER_PRIVATE_KEY", raw);
    }

    [Fact]
    public async Task TestConnection_WithoutProvider_ReturnsBadRequest()
    {
        var (factory, ownerId, _) = NewFactoryWithUsers();
        await using var _f = factory;

        var asOwner = AsUser(factory, ownerId, AppRoles.Owner);
        var response = await asOwner.PostAsync("/api/settings/sso/test", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
