using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public class EmailServiceTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private class MockEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string cipherText) => cipherText;
    }

    [Fact]
    public async Task IsConfiguredAsync_NoSettings_ReturnsFalse()
    {
        var db = GetDbContext();
        var enc = new MockEncryptionService();
        var svc = new EmailService(db, enc, NullLogger<EmailService>.Instance);

        var result = await svc.IsConfiguredAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task IsConfiguredAsync_ValidSettings_ReturnsTrue()
    {
        var db = GetDbContext();
        db.SmtpSettings.Add(new SmtpSettings { Enabled = true, Host = "smtp.example.com", FromAddress = "no-reply@example.com" });
        await db.SaveChangesAsync();

        var enc = new MockEncryptionService();
        var svc = new EmailService(db, enc, NullLogger<EmailService>.Instance);

        var result = await svc.IsConfiguredAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task SendAsync_NoSettings_ReturnsNotConfigured()
    {
        var db = GetDbContext();
        var enc = new MockEncryptionService();
        var svc = new EmailService(db, enc, NullLogger<EmailService>.Instance);

        var result = await svc.SendTestAsync("test@x");
        Assert.False(result.Success);
        Assert.Equal("SMTP relay is not configured.", result.Error);
    }

    [Fact]
    public async Task SendAsync_DummyHost_ThrowsAndReturnsFalse()
    {
        var db = GetDbContext();
        db.SmtpSettings.Add(new SmtpSettings { Enabled = true, Host = "invalid.host.that.does.not.exist.local", Port = 25, FromAddress = "a@a.com" });
        await db.SaveChangesAsync();

        var enc = new MockEncryptionService();
        var svc = new EmailService(db, enc, NullLogger<EmailService>.Instance);

        var result = await svc.SendInviteAsync("test@x", "Test", "http://x");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);

        var result2 = await svc.SendPasswordResetAsync("test@x", "Test", "http://x");
        Assert.False(result2.Success);
    }

    [Fact]
    public async Task SendAsync_WithCredentials_AttemptsToConnectAndReturnsFalse()
    {
        var db = GetDbContext();
        db.SmtpSettings.Add(new SmtpSettings
        {
            Enabled = true,
            Host = "invalid.host.that.does.not.exist.local",
            Port = 25,
            FromAddress = "a@a.com",
            Username = "user",
            EncryptedPassword = "encrypted"
        });
        await db.SaveChangesAsync();

        var enc = new MockEncryptionService();
        var svc = new EmailService(db, enc, NullLogger<EmailService>.Instance);

        var result = await svc.SendTestAsync("test@x");
        Assert.False(result.Success);
    }
}
