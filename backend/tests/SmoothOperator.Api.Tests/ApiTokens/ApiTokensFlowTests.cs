using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Api.Tests.ApiTokens;

public class ApiTokensFlowTests
{
    private const string DefaultPassword = "Password123!!";

    private static void SeedUser(AppDbContext db, Guid userId, string email = "pat-user@example.com", string role = "Owner")
    {
        var ownerRole = db.Roles.First(r => r.Name == role);
        var user = new User
        {
            Id = userId,
            Email = email,
            Name = "PAT Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
        };
        user.Roles.Add(ownerRole);
        db.Users.Add(user);
    }

    private static HttpClient AsUser(TestWebApplicationFactory factory, Guid userId, string role = "Owner")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static async Task<(Guid Id, string Plaintext)> CreateTokenAsync(HttpClient client, string name = "ci-bot")
    {
        var res = await client.PostAsJsonAsync("/api/api-tokens", new { name });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return (
            Id: doc.RootElement.GetProperty("id").GetGuid(),
            Plaintext: doc.RootElement.GetProperty("plaintextToken").GetString()!
        );
    }

    [Fact]
    public async Task Create_ReturnsPlaintextOnce_HashPersisted()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var (id, plaintext) = await CreateTokenAsync(client, "ci-bot");
        Assert.StartsWith("sop_", plaintext);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = db.ApiTokens.Single(t => t.Id == id);
        Assert.NotEqual(plaintext, token.TokenHash);
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
        Assert.Equal(expected, token.TokenHash);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409Conflict()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var first = await client.PostAsJsonAsync("/api/api-tokens", new { name = "ci-bot" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var dup = await client.PostAsJsonAsync("/api/api-tokens", new { name = "ci-bot" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Create_WithBareIsoDateExpiresAt_PersistsAsUtc()
    {
        // Reproduces the frontend payload shape: ExpiresAt arrives as a JSON string
        // ("2027-01-01") with no offset, which model binding produces as
        // DateTime.Kind=Unspecified. Postgres timestamptz only accepts UTC, so
        // the handler must coerce before persistence.
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var res = await client.PostAsJsonAsync("/api/api-tokens", new
        {
            name = "with-expiry",
            expiresAt = "2099-01-01",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = db.ApiTokens.Single(t => t.Id == id);
        Assert.NotNull(token.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, token.ExpiresAt.Value.Kind);
    }

    [Fact]
    public async Task Create_PastExpiresAt_Returns400BadRequest()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var res = await client.PostAsJsonAsync("/api/api-tokens", new
        {
            name = "expired-on-create",
            expiresAt = DateTime.UtcNow.AddDays(-1),
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task List_DoesNotReturnHashOrLookup()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        await CreateTokenAsync(client);

        var res = await client.GetAsync("/api/api-tokens");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenLookup", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plaintext", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthHeaderWithValidToken_GrantsAccessAsOwner()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (_, plaintext) = await CreateTokenAsync(client);

        var patClient = factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        // Hit a JWT-only endpoint that returns the current user's profile.
        var meRes = await patClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        using var doc = JsonDocument.Parse(await meRes.Content.ReadAsStringAsync());
        Assert.Equal(userId.ToString(), doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task AuthHeaderWithRevokedToken_Returns401()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, plaintext) = await CreateTokenAsync(client);

        var revoke = await client.DeleteAsync($"/api/api-tokens/{id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var patClient = factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var meRes = await patClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meRes.StatusCode);
    }

    [Fact]
    public async Task AuthHeaderWithUnknownPrefix_Returns401()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var patClient = factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "sop_unknownlookup_unknownsecret");

        var meRes = await patClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meRes.StatusCode);
    }

    [Fact]
    public async Task AuthHeaderWithMalformedToken_Returns401()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var patClient = factory.CreateClient();
        // "sop_" prefix matches but no second underscore — handler short-circuits to Fail.
        patClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "sop_missingsecond");

        var res = await patClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AuthHeaderWithExpiredToken_Returns401()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, plaintext) = await CreateTokenAsync(client, "expires-soon");

        // Backdate ExpiresAt to make this PAT immediately expired.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = db.ApiTokens.Single(t => t.Id == id);
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var patClient = factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var res = await patClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AuthHeaderWithCorruptedSecret_Returns401()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (_, plaintext) = await CreateTokenAsync(client);

        // Flip one character in the secret half; lookup still resolves, hash compare fails.
        var firstUnderscoreAfterPrefix = plaintext.IndexOf('_', 4);
        var corrupted = plaintext[..(firstUnderscoreAfterPrefix + 1)]
            + (plaintext[firstUnderscoreAfterPrefix + 1] == 'A' ? 'B' : 'A')
            + plaintext[(firstUnderscoreAfterPrefix + 2)..];

        var patClient = factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", corrupted);

        var res = await patClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task PatCannotCreateAnotherPat()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (_, plaintext) = await CreateTokenAsync(client, "first");

        var patClient = factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var res = await patClient.PostAsJsonAsync("/api/api-tokens", new { name = "loop" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Revoke_NonExistent_Returns404()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);

        var res = await client.DeleteAsync($"/api/api-tokens/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Revoke_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db => SeedUser(db, userId));
        var client = AsUser(factory, userId);
        var (id, _) = await CreateTokenAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/api-tokens/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/api-tokens/{id}")).StatusCode);
    }
}
