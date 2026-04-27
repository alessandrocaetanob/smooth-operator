using System.Net;
using System.Net.Http.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests.Controllers;

public class CredentialsControllerTests
{
    private static void AttachRoles(AppDbContext db, User user, params string[] roleNames)
    {
        foreach (var name in roleNames)
        {
            user.Roles.Add(db.Roles.First(r => r.Name == name));
        }
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (roles.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    // A factory wrapper to ensure ENCRYPTION_KEY is set in environment for these tests
    private static TestWebApplicationFactory CreateFactory(Action<AppDbContext>? seed = null)
    {
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", new string('a', 64));
        return new TestWebApplicationFactory(seed);
    }

    [Theory]
    [InlineData(AppRoles.Owner)]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.TeamAdmin)]
    public async Task GetCredentials_AuthorizedRoles_ReturnsCredentialsWithoutSecrets(string role)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var credentialId1 = Guid.NewGuid();
        var credentialId2 = Guid.NewGuid();

        await using var factory = CreateFactory(db =>
        {
            var user = new User { Id = userId, Email = $"test_{role}@test.com", Name = $"Test {role}", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, role);
            db.Users.Add(user);

            db.Credentials.AddRange(
                new Credential { Id = credentialId1, Name = "Cred 1", Username = "user1", CredentialType = "password", EncryptedSecret = "encrypted1", PublicKey = "pub1" },
                new Credential { Id = credentialId2, Name = "Cred 2", Username = "user2", CredentialType = "private_key", EncryptedSecret = "encrypted2", PublicKey = "pub2" }
            );
        });

        var client = AsUser(factory, userId, role);

        // Act
        var response = await client.GetAsync("/api/credentials");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var credentials = await response.Content.ReadFromJsonAsync<List<CredentialDto>>();
        Assert.NotNull(credentials);
        Assert.Equal(2, credentials.Count);

        var cred1 = credentials.Single(c => c.Id == credentialId1);
        Assert.Equal("Cred 1", cred1.Name);
        Assert.Equal("user1", cred1.Username);
        Assert.Equal("password", cred1.CredentialType);
        Assert.Equal("pub1", cred1.PublicKey);

        var cred2 = credentials.Single(c => c.Id == credentialId2);
        Assert.Equal("Cred 2", cred2.Name);
        Assert.Equal("user2", cred2.Username);
        Assert.Equal("private_key", cred2.CredentialType);
        Assert.Equal("pub2", cred2.PublicKey);
    }

    [Fact]
    public async Task GetCredentials_UserRole_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();

        await using var factory = CreateFactory(db =>
        {
            var user = new User { Id = userId, Email = "test_user@test.com", Name = "Test User", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            db.Credentials.Add(
                new Credential { Id = Guid.NewGuid(), Name = "Cred 1", Username = "user1", CredentialType = "password", EncryptedSecret = "encrypted1" }
            );
        });

        var client = AsUser(factory, userId, AppRoles.User);

        // Act
        var response = await client.GetAsync("/api/credentials");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = CreateFactory(db =>
        {
            db.Credentials.Add(
                new Credential { Id = Guid.NewGuid(), Name = "Cred 1", Username = "user1", CredentialType = "password", EncryptedSecret = "encrypted1" }
            );
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/credentials");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        await using var factory = CreateFactory(db =>
        {
            var user = new User { Id = userId, Email = "test_admin@test.com", Name = "Test Admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.Admin);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.Admin);

        // Act
        var response = await client.GetAsync("/api/credentials");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var credentials = await response.Content.ReadFromJsonAsync<List<CredentialDto>>();
        Assert.NotNull(credentials);
        Assert.Empty(credentials);
    }
}
