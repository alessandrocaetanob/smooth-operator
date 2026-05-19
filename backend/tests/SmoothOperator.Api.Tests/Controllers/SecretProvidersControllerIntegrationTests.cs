using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class SecretProvidersControllerIntegrationTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
            user.Roles.Add(db.Roles.First(r => r.Name == name));
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    private static CreateSecretProviderDto ValidCreateDto(string name = "My KV") => new()
    {
        Name = name,
        Type = SecretProviderType.AzureKeyVault,
        IsEnabled = true,
        TenantId = "tenant-id",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        VaultUri = "https://my-vault.vault.azure.net/"
    };

    // ── RBAC ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_AsRegularUser_Returns403()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.GetAsync("/api/secretproviders");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsTeamAdmin_Returns403()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var u = new User { Id = userId, Email = "ta@x", Name = "ta", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.TeamAdmin);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.TeamAdmin);
        var res = await client.PostAsJsonAsync("/api/secretproviders", ValidCreateDto());

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── CRUD lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public async Task SecretProviders_CrudLifecycle()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. List — empty
        var listRes = await client.GetAsync("/api/secretproviders");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var emptyList = await listRes.Content.ReadFromJsonAsync<List<SecretProviderDto>>();
        Assert.NotNull(emptyList);
        Assert.Empty(emptyList);

        // 2. Create
        var createRes = await client.PostAsJsonAsync("/api/secretproviders", ValidCreateDto("Test KV"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SecretProviderDto>();
        Assert.NotNull(created);
        Assert.Equal("Test KV", created.Name);
        Assert.Equal("AzureKeyVault", created.Type);
        Assert.True(created.IsEnabled);
        Assert.True(created.IsConfigured);
        Assert.True(created.HasClientSecret);
        Assert.Equal("https://my-vault.vault.azure.net/", created.VaultUri);
        Assert.Equal("tenant-id", created.TenantId);
        Assert.Equal("client-id", created.ClientId);
        var providerId = created.Id;

        // 3. Get by id
        var getRes = await client.GetAsync($"/api/secretproviders/{providerId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<SecretProviderDto>();
        Assert.NotNull(fetched);
        Assert.Equal(providerId, fetched.Id);
        Assert.Equal("Test KV", fetched.Name);

        // 4. List — contains created
        var listRes2 = await client.GetAsync("/api/secretproviders");
        Assert.Equal(HttpStatusCode.OK, listRes2.StatusCode);
        var list2 = await listRes2.Content.ReadFromJsonAsync<List<SecretProviderDto>>();
        Assert.NotNull(list2);
        Assert.Contains(list2, p => p.Id == providerId);

        // 5. Update — rename + disable + change vault URI
        var updateDto = new UpdateSecretProviderDto
        {
            Name = "Updated KV",
            IsEnabled = false,
            VaultUri = "https://updated-vault.vault.azure.net/"
        };
        var updateRes = await client.PutAsJsonAsync($"/api/secretproviders/{providerId}", updateDto);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<SecretProviderDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated KV", updated.Name);
        Assert.False(updated.IsEnabled);
        Assert.Equal("https://updated-vault.vault.azure.net/", updated.VaultUri);

        // 6. Delete
        var deleteRes = await client.DeleteAsync($"/api/secretproviders/{providerId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // 7. Get after delete — 404
        var getAfterDelete = await client.GetAsync($"/api/secretproviders/{providerId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_MissingRequiredFields_Returns400()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        var badDto = new CreateSecretProviderDto
        {
            Name = "Missing Fields",
            Type = SecretProviderType.AzureKeyVault,
            // TenantId, ClientId, ClientSecret, VaultUri all empty
        };

        var res = await client.PostAsJsonAsync("/api/secretproviders", badDto);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── Test-connection (mocked factory) ─────────────────────────────────────

    [Fact]
    public async Task TestConnection_SuccessfulProvider_ReturnsTrue()
    {
        var adminId = Guid.NewGuid();
        var mockProvider = new Mock<ISecretProvider>();
        mockProvider.Setup(p => p.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockFactory = new Mock<ISecretProviderFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<SecretProvider>()))
            .Returns(mockProvider.Object);

        await using var factory = new TestWebApplicationFactory(
            seed: db =>
            {
                var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, admin, AppRoles.Admin);
                db.Users.Add(admin);
            },
            overrideServices: services =>
            {
                services.RemoveAll<ISecretProviderFactory>();
                services.AddSingleton(mockFactory.Object);
            });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // Create a provider
        var createRes = await client.PostAsJsonAsync("/api/secretproviders", ValidCreateDto("TC Provider"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SecretProviderDto>();
        Assert.NotNull(created);

        var testRes = await client.PostAsync($"/api/secretproviders/{created.Id}/test", null);
        Assert.Equal(HttpStatusCode.OK, testRes.StatusCode);

        var json = await testRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task TestConnection_FailingProvider_Returns500()
    {
        var adminId = Guid.NewGuid();
        var mockProvider = new Mock<ISecretProvider>();
        mockProvider.Setup(p => p.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated vault failure"));
        var mockFactory = new Mock<ISecretProviderFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<SecretProvider>()))
            .Returns(mockProvider.Object);

        await using var factory = new TestWebApplicationFactory(
            seed: db =>
            {
                var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, admin, AppRoles.Admin);
                db.Users.Add(admin);
            },
            overrideServices: services =>
            {
                services.RemoveAll<ISecretProviderFactory>();
                services.AddSingleton(mockFactory.Object);
            });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        var createRes = await client.PostAsJsonAsync("/api/secretproviders", ValidCreateDto("Broken KV"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SecretProviderDto>();
        Assert.NotNull(created);

        var testRes = await client.PostAsync($"/api/secretproviders/{created.Id}/test", null);
        // InvalidOperationException maps to 500 in the exception handling middleware
        Assert.Equal(HttpStatusCode.InternalServerError, testRes.StatusCode);
    }

    [Fact]
    public async Task TestConnection_NonExistentProvider_Returns404()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.PostAsync($"/api/secretproviders/{Guid.NewGuid()}/test", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ListSecrets_AsRegularUser_Returns403()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.GetAsync($"/api/secretproviders/{Guid.NewGuid()}/secrets");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ListSecrets_NonExistentProvider_Returns404()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var res = await client.GetAsync($"/api/secretproviders/{Guid.NewGuid()}/secrets");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ListSecrets_ExistingProvider_ReturnsNamesFromProvider()
    {
        var adminId = Guid.NewGuid();
        var mockProvider = new Mock<ISecretProvider>();
        mockProvider.Setup(p => p.ListSecretNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["db-password", "smtp-key"]);
        var mockFactory = new Mock<ISecretProviderFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<SecretProvider>()))
            .Returns(mockProvider.Object);

        await using var factory = new TestWebApplicationFactory(
            seed: db =>
            {
                var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, admin, AppRoles.Admin);
                db.Users.Add(admin);
            },
            overrideServices: services =>
            {
                services.RemoveAll<ISecretProviderFactory>();
                services.AddSingleton(mockFactory.Object);
            });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var createRes = await client.PostAsJsonAsync("/api/secretproviders", ValidCreateDto("Secrets KV"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SecretProviderDto>();
        Assert.NotNull(created);

        var listRes = await client.GetAsync($"/api/secretproviders/{created.Id}/secrets");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var names = await listRes.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(names);
        Assert.Equal(2, names.Count);
        Assert.Contains("db-password", names);
        Assert.Contains("smtp-key", names);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
}
