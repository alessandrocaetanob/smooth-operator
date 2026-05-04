using System.Net;
using System.Net.Http.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests.Controllers;

public class UsersControllerLifecycleTests
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
    public async Task Users_Lifecycle()
    {
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin, AppRoles.Owner);
            db.Users.Add(admin);

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin, AppRoles.Owner);

        // 1. Get All Users
        var getAllRes = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, getAllRes.StatusCode);
        var users = await getAllRes.Content.ReadFromJsonAsync<List<UserListItemDto>>();
        Assert.NotNull(users);
        Assert.Contains(users, u => u.Id == userId);

        // 2. Get Single User
        var getOneRes = await client.GetAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getOneRes.StatusCode);
        var one = await getOneRes.Content.ReadFromJsonAsync<UserListItemDto>();
        Assert.NotNull(one);
        Assert.Equal("u", one.Name);

        // 3. Update User
        var updateReq = new UpdateUserRequest { Name = "u updated" };
        var updateRes = await client.PutAsJsonAsync($"/api/users/{userId}", updateReq);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        // 4. Set Role
        var setRoleReq = new SetUserRoleRequest { Role = AppRoles.TeamAdmin };
        var setRoleRes = await client.PutAsJsonAsync($"/api/users/{userId}/role", setRoleReq);
        Assert.Equal(HttpStatusCode.NoContent, setRoleRes.StatusCode);

        // 5. Set Active
        var setActiveReq = new SetUserActiveRequest { IsActive = false };
        var setActiveRes = await client.PatchAsJsonAsync($"/api/users/{userId}/active", setActiveReq);
        Assert.Equal(HttpStatusCode.NoContent, setActiveRes.StatusCode);

        // 6. Delete User
        var delRes = await client.DeleteAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    public async Task Self_Profile_Update_And_GetVaults()
    {
        var userId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "self@x", Name = "self", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);

        // Get effective vaults
        var getVaultsRes = await client.GetAsync($"/api/users/{userId}/effective-vaults");
        Assert.Equal(HttpStatusCode.OK, getVaultsRes.StatusCode);

        // Update profile
        var updateReq = new UpdateProfileRequest { Name = "self updated", AvatarBase64 = "YmFzZTY0", AvatarMimeType = "image/png" };
        var updateRes = await client.PutAsJsonAsync("/api/users/me/profile", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<UserInfo>();
        Assert.NotNull(updated);
        Assert.Equal("self updated", updated.Name);

        // Delete avatar
        var delAvatarRes = await client.DeleteAsync("/api/users/me/avatar");
        Assert.Equal(HttpStatusCode.OK, delAvatarRes.StatusCode);
    }

    [Fact]
    public async Task Get_Roles()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@x", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Owner);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Owner);
        var rolesRes = await client.GetAsync("/api/users/roles");
        Assert.Equal(HttpStatusCode.OK, rolesRes.StatusCode);
        var roles = await rolesRes.Content.ReadFromJsonAsync<List<RoleCatalogItemDto>>();
        Assert.NotEmpty(roles);
    }
}
