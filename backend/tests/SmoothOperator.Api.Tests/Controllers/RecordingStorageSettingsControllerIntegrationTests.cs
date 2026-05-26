using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class RecordingStorageSettingsControllerIntegrationTests
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

    [Fact]
    public async Task Get_Update_RoundTrip_HidesSecretsAndEvictsCache()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });
        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Initial GET — nothing configured yet.
        var initial = await client.GetFromJsonAsync<RecordingStorageSettingsDto>("/api/settings/recording-storage", TestJson.Options);
        Assert.NotNull(initial);
        Assert.False(initial!.Configured);

        // 2. PUT with S3 config including a secret key.
        var put = await client.PutAsJsonAsync("/api/settings/recording-storage", new UpdateRecordingStorageSettingsRequest
        {
            StorageType = RecordingStorageType.S3,
            LocalPath = "/var/recordings",
            S3Bucket = "my-bucket",
            S3Region = "us-east-1",
            S3AccessKeyId = "AKIAEXAMPLE",
            S3SecretAccessKey = "supersecret",
            RetentionDays = 30,
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var afterPut = await put.Content.ReadFromJsonAsync<RecordingStorageSettingsDto>(TestJson.Options);
        Assert.NotNull(afterPut);
        Assert.True(afterPut!.Configured);
        Assert.Equal(RecordingStorageType.S3, afterPut.StorageType);
        Assert.True(afterPut.HasS3SecretAccessKey);
        Assert.Null(GetSecretField(afterPut)); // sanity: no secret leak on DTO

        // 3. Subsequent GET also reflects new state (cache evicted on PUT).
        var afterGet = await client.GetFromJsonAsync<RecordingStorageSettingsDto>("/api/settings/recording-storage", TestJson.Options);
        Assert.NotNull(afterGet);
        Assert.Equal("my-bucket", afterGet!.S3Bucket);
        Assert.True(afterGet.HasS3SecretAccessKey);
        Assert.Equal(30, afterGet.RetentionDays);

        // 4. Verify the secret is actually encrypted at rest (never stored plain).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.RecordingStorageSettings.Single();
        Assert.False(string.IsNullOrEmpty(row.EncryptedS3SecretAccessKey));
        Assert.DoesNotContain("supersecret", row.EncryptedS3SecretAccessKey);
    }

    [Fact]
    public async Task NonAdmin_CannotAccessSettings()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });
        var client = AsUser(factory, userId, AppRoles.User);

        var res = await client.GetAsync("/api/settings/recording-storage");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // Reflection probe to confirm no field in the DTO accidentally surfaces the plaintext secret.
    private static string? GetSecretField(RecordingStorageSettingsDto dto)
    {
        foreach (var prop in typeof(RecordingStorageSettingsDto).GetProperties())
        {
            var v = prop.GetValue(dto) as string;
            if (v != null && v.Contains("supersecret")) return v;
        }
        return null;
    }
}
