using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

public class UserGroupsControllerIntegrationTests
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
    public async Task Create_Updates_And_Deletes_Group()
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // 1. Create
        var createReq = new CreateUserGroupRequest { Name = "Backend Devs", Description = "Backend Developers", OwnerUserId = adminId };
        var createRes = await client.PostAsJsonAsync("/api/groups", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdGroup = await createRes.Content.ReadFromJsonAsync<UserGroupDto>();
        Assert.NotNull(createdGroup);
        Assert.Equal("Backend Devs", createdGroup.Name);
        var groupId = createdGroup.Id;

        // 2. Get
        var getRes = await client.GetAsync($"/api/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetchedGroup = await getRes.Content.ReadFromJsonAsync<UserGroupDto>();
        Assert.NotNull(fetchedGroup);
        Assert.Equal(groupId, fetchedGroup.Id);

        // 3. Update
        var updateReq = new RenameUserGroupRequest { Name = "Core Devs", Description = "Core Team", OwnerUserId = adminId };
        var updateRes = await client.PutAsJsonAsync($"/api/groups/{groupId}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedGroup = await updateRes.Content.ReadFromJsonAsync<UserGroupDto>();
        Assert.NotNull(updatedGroup);
        Assert.Equal("Core Devs", updatedGroup.Name);

        // 4. List
        var listRes = await client.GetAsync("/api/groups");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<UserGroupDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, g => g.Id == groupId);

        // 5. Delete
        var delRes = await client.DeleteAsync($"/api/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // Ensure deleted
        var getResAfterDel = await client.GetAsync($"/api/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NotFound, getResAfterDel.StatusCode);
    }

    [Fact]
    public async Task SetMembers_Adds_And_Removes_Members()
    {
        var adminId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "a@x", Name = "a", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, admin, AppRoles.Admin);
            db.Users.Add(admin);

            db.Users.Add(new User { Id = user1Id, Email = "u1@x", Name = "u1", IsActive = true, CreatedAt = DateTime.UtcNow });
            db.Users.Add(new User { Id = user2Id, Email = "u2@x", Name = "u2", IsActive = true, CreatedAt = DateTime.UtcNow });

            db.UserGroups.Add(new UserGroup { Id = groupId, Name = "Test Group" });
        });

        var client = AsUser(factory, adminId, AppRoles.Admin);

        // Add user1 and user2
        var setRes1 = await client.PutAsJsonAsync($"/api/groups/{groupId}/members", new SetUserGroupMembersRequest { UserIds = new List<Guid> { user1Id, user2Id } });
        Assert.Equal(HttpStatusCode.NoContent, setRes1.StatusCode);

        var getRes1 = await client.GetAsync($"/api/groups/{groupId}");
        var group1 = await getRes1.Content.ReadFromJsonAsync<UserGroupDto>();
        Assert.NotNull(group1);
        Assert.Equal(2, group1.MemberCount);

        // Remove user1, keep user2
        var setRes2 = await client.PutAsJsonAsync($"/api/groups/{groupId}/members", new SetUserGroupMembersRequest { UserIds = new List<Guid> { user2Id } });
        Assert.Equal(HttpStatusCode.NoContent, setRes2.StatusCode);

        var getRes2 = await client.GetAsync($"/api/groups/{groupId}");
        var group2 = await getRes2.Content.ReadFromJsonAsync<UserGroupDto>();
        Assert.NotNull(group2);
        Assert.Equal(1, group2.MemberCount);
        Assert.Equal(user2Id, group2.Members[0].Id);
    }
}
