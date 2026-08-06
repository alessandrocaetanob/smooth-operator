using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Tests.Infrastructure;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Tests.Controllers;

public class InvitesControllerIntegrationTests
{
    [Fact]
    public async Task Preview_ReturnsInviteDetails_ForValidToken()
    {
        var userId = Guid.NewGuid();
        const string rawToken = "valid-invite-token";

        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User
            {
                Id = userId,
                Email = "invitee@x",
                Name = "Invitee User",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });

            db.Invitations.Add(new Invitation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = InviteService.TypeUserInvite,
                TokenHash = InviteService.HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/invites/{rawToken}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<SmoothOperator.Application.DTOs.InvitePreviewDto>();
        Assert.NotNull(dto);
        Assert.Equal("invitee@x", dto.Email);
        Assert.Equal("Invitee User", dto.Name);
        Assert.Equal(InviteService.TypeUserInvite, dto.Type);
    }

    [Fact]
    public async Task Preview_ReturnsNotFound_ForInvalidToken()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/invites/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Redeem_Succeeds_AndUpdatesNameWhenProvided()
    {
        var userId = Guid.NewGuid();
        const string rawToken = "redeem-with-name-token";

        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User
            {
                Id = userId,
                Email = "invitee@example.com",
                Name = "Placeholder",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            db.Invitations.Add(new Invitation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = InviteService.TypeUserInvite,
                TokenHash = InviteService.HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow
            });
        });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/invites/{rawToken}/redeem",
            new { Password = "Str0ng!P@ssword1", Name = "Updated Name" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(userId);
        Assert.Equal("Updated Name", user!.Name);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Redeem_Returns404_ForInvalidToken()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/invites/no-such-token/redeem",
            new { Password = "Str0ng!P@ssword1", Name = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
