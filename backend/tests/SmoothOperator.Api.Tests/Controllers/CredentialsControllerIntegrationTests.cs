using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Controllers;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class CredentialsControllerIntegrationTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
        {
            var role = db.Roles.First(r => r.Name == name);
            user.Roles.Add(role);
        }
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    [Fact]
    public async Task Credentials_Lifecycle()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Create Password
        var createReq = new CreateCredentialDto
        {
            Name = "DB Pass",
            Username = "postgres",
            Secret = "supersecret",
            CredentialType = "password"
        };
        var createRes = await client.PostAsJsonAsync("/api/credentials", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<CredentialDto>();
        Assert.NotNull(created);
        Assert.Equal("DB Pass", created.Name);
        var credId = created.Id;

        // 2. List
        var listRes = await client.GetAsync("/api/credentials");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<CredentialDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, c => c.Id == credId);

        // 3. Update Password
        var updateReq = new CreateCredentialDto
        {
            Name = "DB Pass updated",
            Username = "postgres_adm",
            Secret = "newsecret",
            CredentialType = "password"
        };
        var updateRes = await client.PutAsJsonAsync($"/api/credentials/{credId}", updateReq);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        // 4. Delete
        var delRes = await client.DeleteAsync($"/api/credentials/{credId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    public async Task GenerateSshKey_Rsa_And_Ecdsa()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // RSA
        var rsaReq = new GenerateSshKeyRequest { KeyType = "rsa" };
        var rsaRes = await client.PostAsJsonAsync("/api/credentials/generate-ssh", rsaReq);
        Assert.Equal(HttpStatusCode.OK, rsaRes.StatusCode);
        var rsaData = await rsaRes.Content.ReadFromJsonAsync<GenerateSshKeyResponse>();
        Assert.NotNull(rsaData);
        Assert.StartsWith("-----BEGIN RSA PRIVATE KEY-----", rsaData.PrivateKey);
        Assert.StartsWith("ssh-rsa ", rsaData.PublicKey);

        // ECDSA
        var ecdsaReq = new GenerateSshKeyRequest { KeyType = "ecdsa" };
        var ecdsaRes = await client.PostAsJsonAsync("/api/credentials/generate-ssh", ecdsaReq);
        Assert.Equal(HttpStatusCode.OK, ecdsaRes.StatusCode);
        var ecdsaData = await ecdsaRes.Content.ReadFromJsonAsync<GenerateSshKeyResponse>();
        Assert.NotNull(ecdsaData);
        Assert.StartsWith("-----BEGIN EC PRIVATE KEY-----", ecdsaData.PrivateKey);
        Assert.StartsWith("ecdsa-sha2-nistp256 ", ecdsaData.PublicKey);
    }

    [Fact]
    public async Task CreateCredential_ExternalPushToVault_PersistsReferenceAndCallsProvider()
    {
        var adminId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var mockProvider = new Mock<ISecretProvider>();
        mockProvider
            .Setup(p => p.SetSecretAsync(It.IsAny<string>(), "vault-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var mockFactory = new Mock<ISecretProviderFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<SecretProvider>())).Returns(mockProvider.Object);

        await using var factory = new TestWebApplicationFactory(
            seed: db =>
            {
                var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
                AttachRoles(db, admin, AppRoles.Admin);
                db.Users.Add(admin);
                db.SecretProviders.Add(new SecretProvider
                {
                    Id = providerId,
                    Name = "KV",
                    Type = SecretProviderType.AzureKeyVault,
                    EncryptedConfigJson = "{}",
                    IsEnabled = true
                });
            },
            overrideServices: services =>
            {
                services.RemoveAll<ISecretProviderFactory>();
                services.AddSingleton(mockFactory.Object);
            });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var request = new CreateCredentialDto
        {
            Name = "external-push",
            Username = "svc-user",
            CredentialType = "password",
            StorageMode = "External",
            SecretProviderId = providerId,
            PushToVault = true,
            Secret = "vault-secret"
        };

        var response = await client.PostAsJsonAsync("/api/credentials", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CredentialDto>();
        Assert.NotNull(created);
        Assert.Equal("External", created.StorageMode);
        Assert.Equal(providerId, created.SecretProviderId);
        Assert.False(string.IsNullOrWhiteSpace(created.ExternalSecretName));
        Assert.Null(created.ExternalSecretVersion);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Credentials.FindAsync(created.Id);
        Assert.NotNull(persisted);
        Assert.Equal(SecretStorageMode.External, persisted.StorageMode);
        Assert.Equal(string.Empty, persisted.EncryptedSecret);
        Assert.Equal(providerId, persisted.SecretProviderId);
        Assert.False(string.IsNullOrWhiteSpace(persisted.ExternalSecretName));

        mockProvider.Verify(p => p.SetSecretAsync(
            It.Is<string>(n => n.StartsWith("smoothoperator-external-push-")),
            "vault-secret",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCredential_ExternalLink_PersistsProvidedSecretReference()
    {
        var adminId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
            db.SecretProviders.Add(new SecretProvider
            {
                Id = providerId,
                Name = "KV",
                Type = SecretProviderType.AzureKeyVault,
                EncryptedConfigJson = "{}",
                IsEnabled = true
            });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var request = new CreateCredentialDto
        {
            Name = "external-link",
            Username = "svc-user",
            CredentialType = "password",
            StorageMode = "External",
            SecretProviderId = providerId,
            PushToVault = false,
            ExternalSecretName = "existing-secret",
            ExternalSecretVersion = "v2"
        };

        var response = await client.PostAsJsonAsync("/api/credentials", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CredentialDto>();
        Assert.NotNull(created);
        Assert.Equal("existing-secret", created.ExternalSecretName);
        Assert.Equal("v2", created.ExternalSecretVersion);
    }

    [Fact]
    public async Task CreateCredential_ExternalWithoutProviderId_ReturnsBadRequest()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var request = new CreateCredentialDto
        {
            Name = "invalid-external",
            Username = "svc-user",
            CredentialType = "password",
            StorageMode = "External",
            PushToVault = false,
            ExternalSecretName = "existing-secret"
        };

        var response = await client.PostAsJsonAsync("/api/credentials", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCredential_ExternalWithUnknownProvider_ReturnsNotFound()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);
        var request = new CreateCredentialDto
        {
            Name = "unknown-provider",
            Username = "svc-user",
            CredentialType = "password",
            StorageMode = "External",
            SecretProviderId = Guid.NewGuid(),
            PushToVault = false,
            ExternalSecretName = "existing-secret"
        };

        var response = await client.PostAsJsonAsync("/api/credentials", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
