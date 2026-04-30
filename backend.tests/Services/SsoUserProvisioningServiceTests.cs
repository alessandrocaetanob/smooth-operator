using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Sso;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests.Services;

public class SsoUserProvisioningServiceTests
{
    private static AppDbContext NewContext()
    {
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        // Seed required default role.
        ctx.Roles.Add(new Role { Id = Guid.NewGuid(), Name = AppRoles.User });
        ctx.Roles.Add(new Role { Id = Guid.NewGuid(), Name = AppRoles.Admin });
        ctx.SaveChanges();
        return ctx;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public TimeSpan TokenLifetime => TimeSpan.FromMinutes(5);
        public List<User> Issued { get; } = new();
        public string CreateToken(User user)
        {
            Issued.Add(user);
            return $"token-for-{user.Id}";
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public List<(string Action, string ResourceType, string ResourceId, object? Details)> Events { get; } = new();
        public Task WriteAsync(string action, string resourceType, string resourceId, object? details = null)
        {
            Events.Add((action, resourceType, resourceId, details));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProvisionOrLinkAsync_CreatesNewUserWithDefaultRole()
    {
        await using var ctx = NewContext();
        var tokens = new FakeTokenService();
        var audit = new FakeAuditService();
        var svc = new SsoUserProvisioningService(ctx, tokens, audit);

        var result = await svc.ProvisionOrLinkAsync(SsoProviderType.Oidc, "ext-1", "Alice@Example.com", "Alice");

        Assert.True(result.WasCreated);
        Assert.False(result.WasLinked);
        Assert.Equal("alice@example.com", result.User.Email);
        Assert.Equal("Alice", result.User.Name);
        Assert.Equal("ext-1", result.User.ExternalId);
        Assert.Equal(SsoProviderType.Oidc, result.User.SsoProviderType);
        Assert.Contains(result.User.Roles, r => r.Name == AppRoles.User);
        Assert.Equal($"token-for-{result.User.Id}", result.Token);
        Assert.Contains(audit.Events, e => e.Action == "user.sso_provisioned");
    }

    [Fact]
    public async Task ProvisionOrLinkAsync_LinksExistingLocalUserByEmail()
    {
        await using var ctx = NewContext();
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "bob@example.com",
            Name = "Bob",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(existing);
        await ctx.SaveChangesAsync();

        var audit = new FakeAuditService();
        var svc = new SsoUserProvisioningService(ctx, new FakeTokenService(), audit);

        var result = await svc.ProvisionOrLinkAsync(SsoProviderType.Saml, "ext-2", "BOB@example.com", null);

        Assert.False(result.WasCreated);
        Assert.True(result.WasLinked);
        Assert.Equal(existing.Id, result.User.Id);
        Assert.Equal("ext-2", result.User.ExternalId);
        Assert.Equal(SsoProviderType.Saml, result.User.SsoProviderType);
        Assert.Contains(audit.Events, e => e.Action == "user.sso_linked");
    }

    [Fact]
    public async Task ProvisionOrLinkAsync_FindsByExternalId_OnReturningLogin()
    {
        await using var ctx = NewContext();
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "carol@example.com",
            Name = "Carol",
            ExternalId = "ext-3",
            SsoProviderType = SsoProviderType.Oidc,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(existing);
        await ctx.SaveChangesAsync();

        var audit = new FakeAuditService();
        var svc = new SsoUserProvisioningService(ctx, new FakeTokenService(), audit);

        // Email differs from stored — ExternalId is the lookup key, not email.
        var result = await svc.ProvisionOrLinkAsync(SsoProviderType.Oidc, "ext-3", "different@example.com", null);

        Assert.False(result.WasCreated);
        Assert.False(result.WasLinked);
        Assert.Equal(existing.Id, result.User.Id);
        Assert.Contains(audit.Events, e => e.Action == "user.sso_login");
    }

    [Fact]
    public async Task ProvisionOrLinkAsync_RejectsDisabledUser_AndAuditsFailure()
    {
        await using var ctx = NewContext();
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "dave@example.com",
            Name = "Dave",
            ExternalId = "ext-4",
            SsoProviderType = SsoProviderType.Oidc,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(existing);
        await ctx.SaveChangesAsync();

        var audit = new FakeAuditService();
        var svc = new SsoUserProvisioningService(ctx, new FakeTokenService(), audit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ProvisionOrLinkAsync(SsoProviderType.Oidc, "ext-4", "dave@example.com", null));

        Assert.Contains(audit.Events, e => e.Action == "sso.login_failed");
    }

    [Theory]
    [InlineData("", "x@y")]
    [InlineData("ext", "")]
    [InlineData("   ", "x@y")]
    public async Task ProvisionOrLinkAsync_ValidatesRequiredInputs(string externalId, string email)
    {
        await using var ctx = NewContext();
        var svc = new SsoUserProvisioningService(ctx, new FakeTokenService(), new FakeAuditService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ProvisionOrLinkAsync(SsoProviderType.Oidc, externalId, email, null));
    }
}
