using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OtpNet;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Api.Tests.Infrastructure;

namespace SmoothOperator.Api.Tests.Controllers;

public class MfaFlowTests
{
    private const string DefaultPassword = "Password123!!";

    private static void SeedUserWithPassword(AppDbContext db, Guid userId, string email, string password = DefaultPassword)
    {
        var ownerRole = db.Roles.First(r => r.Name == "Owner");
        var user = new User
        {
            Id = userId,
            Email = email,
            Name = "MFA Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        };
        user.Roles.Add(ownerRole);
        db.Users.Add(user);
    }

    private static HttpClient AuthenticatedClient(TestWebApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Owner");
        return client;
    }

    private static async Task<(string SecretBase32, List<string> RecoveryCodes)> EnrollAndConfirmAsync(HttpClient client)
    {
        var enrollRes = await client.PostAsync("/api/mfa/enroll", null!);
        Assert.Equal(HttpStatusCode.OK, enrollRes.StatusCode);
        using var enrollDoc = JsonDocument.Parse(await enrollRes.Content.ReadAsStringAsync());
        var secretBase32 = enrollDoc.RootElement.GetProperty("secretBase32").GetString()!;

        var code = new Totp(Base32Encoding.ToBytes(secretBase32)).ComputeTotp();
        var confirmRes = await client.PostAsJsonAsync("/api/mfa/confirm", new { code });
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        using var confirmDoc = JsonDocument.Parse(await confirmRes.Content.ReadAsStringAsync());
        var recoveryCodes = confirmDoc.RootElement.GetProperty("recoveryCodes")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        return (secretBase32, recoveryCodes);
    }

    [Fact]
    public async Task EnrollAndConfirm_ReturnsRecoveryCodes()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
            SeedUserWithPassword(db, userId, "mfa-enroll@example.com"));
        var client = AuthenticatedClient(factory, userId);

        var enrollRes = await client.PostAsync("/api/mfa/enroll", null!);
        Assert.Equal(HttpStatusCode.OK, enrollRes.StatusCode);
        using var enrollDoc = JsonDocument.Parse(await enrollRes.Content.ReadAsStringAsync());
        var secretBase32 = enrollDoc.RootElement.GetProperty("secretBase32").GetString()!;
        Assert.False(string.IsNullOrEmpty(secretBase32));
        Assert.StartsWith("otpauth://totp/", enrollDoc.RootElement.GetProperty("otpAuthUri").GetString());

        var code = new Totp(Base32Encoding.ToBytes(secretBase32)).ComputeTotp();
        var confirmRes = await client.PostAsJsonAsync("/api/mfa/confirm", new { code });
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        using var confirmDoc = JsonDocument.Parse(await confirmRes.Content.ReadAsStringAsync());
        var codes = confirmDoc.RootElement.GetProperty("recoveryCodes").EnumerateArray().ToList();
        Assert.Equal(10, codes.Count);
    }

    [Fact]
    public async Task GetStatus_AfterEnrollment_ReturnsEnabled()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
            SeedUserWithPassword(db, userId, "mfa-status@example.com"));
        var client = AuthenticatedClient(factory, userId);

        await EnrollAndConfirmAsync(client);

        var statusRes = await client.GetAsync("/api/mfa/status");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        using var doc = JsonDocument.Parse(await statusRes.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(10, doc.RootElement.GetProperty("recoveryCodesRemaining").GetInt32());
    }

    [Fact]
    public async Task Login_WithMfaEnabled_ReturnsChallengeToken_NotAuthCookie()
    {
        const string email = "mfa-login@example.com";
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
            SeedUserWithPassword(db, userId, email));

        await EnrollAndConfirmAsync(AuthenticatedClient(factory, userId));

        var anonClient = factory.CreateClient();
        var loginRes = await anonClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = DefaultPassword });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var body = await loginRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SmoothOperator.Api.Extensions.AuthCookieExtensions.CookieName,
            loginRes.Headers.ToString());
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("mfaRequired").GetBoolean());
        var challengeToken = doc.RootElement.GetProperty("challengeToken").GetString()!;
        Assert.False(string.IsNullOrEmpty(challengeToken));
    }

    [Fact]
    public async Task VerifyMfa_WithValidTotp_SetsAuthCookie()
    {
        const string email = "mfa-verify@example.com";
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
            SeedUserWithPassword(db, userId, email));

        var (secretBase32, _) = await EnrollAndConfirmAsync(AuthenticatedClient(factory, userId));

        var anonClient = factory.CreateClient();
        var loginRes = await anonClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = DefaultPassword });
        using var loginDoc = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
        var challengeToken = loginDoc.RootElement.GetProperty("challengeToken").GetString()!;

        var code = new Totp(Base32Encoding.ToBytes(secretBase32)).ComputeTotp();
        var verifyRes = await anonClient.PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken, code });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        Assert.Contains(verifyRes.Headers.GetValues("Set-Cookie"),
            c => c.Contains(SmoothOperator.Api.Extensions.AuthCookieExtensions.CookieName));
    }

    [Fact]
    public async Task VerifyMfa_WithRecoveryCode_Succeeds()
    {
        const string email = "mfa-recovery@example.com";
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
            SeedUserWithPassword(db, userId, email));

        var (_, recoveryCodes) = await EnrollAndConfirmAsync(AuthenticatedClient(factory, userId));

        var anonClient = factory.CreateClient();
        var loginRes = await anonClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = DefaultPassword });
        using var loginDoc = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
        var challengeToken = loginDoc.RootElement.GetProperty("challengeToken").GetString()!;

        var verifyRes = await anonClient.PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken, code = recoveryCodes[0] });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        Assert.Contains(verifyRes.Headers.GetValues("Set-Cookie"),
            c => c.Contains(SmoothOperator.Api.Extensions.AuthCookieExtensions.CookieName));
    }

    [Fact]
    public async Task DisableMfa_WithCorrectPassword_RemovesCredential()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
            SeedUserWithPassword(db, userId, "mfa-disable@example.com"));
        var client = AuthenticatedClient(factory, userId);

        await EnrollAndConfirmAsync(client);

        var disableRes = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/mfa")
        {
            Content = JsonContent.Create(new { password = DefaultPassword }),
        });
        Assert.Equal(HttpStatusCode.NoContent, disableRes.StatusCode);

        var statusRes = await client.GetAsync("/api/mfa/status");
        using var doc = JsonDocument.Parse(await statusRes.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("isEnabled").GetBoolean());
    }
}
