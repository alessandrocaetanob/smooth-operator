using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Controllers;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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
}
