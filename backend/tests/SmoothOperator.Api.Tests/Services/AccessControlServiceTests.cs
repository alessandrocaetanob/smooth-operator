using System.Security.Claims;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Tests.Services;

public class AccessControlServiceTests
{
    private static AppDbContext NewContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext ctx,
        string email,
        string[] roleNames,
        Guid[]? directVaultIds = null,
        Guid[]? viaGroupVaultIds = null)
    {
        var roles = new List<Role>();
        foreach (var name in roleNames)
        {
            var existing = await ctx.Roles.FirstOrDefaultAsync(r => r.Name == name);
            if (existing == null)
            {
                existing = new Role { Id = Guid.NewGuid(), Name = name };
                ctx.Roles.Add(existing);
            }
            roles.Add(existing);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Name = email,
            Roles = roles
        };

        ConnectionGroup GetOrCreateVault(Guid vid)
        {
            var local = ctx.ConnectionGroups.Local.FirstOrDefault(g => g.Id == vid);
            if (local != null) return local;
            var tracked = ctx.ConnectionGroups.FirstOrDefault(g => g.Id == vid);
            if (tracked != null) return tracked;
            var created = new ConnectionGroup { Id = vid, Name = $"v-{vid:N}".Substring(0, 10) };
            ctx.ConnectionGroups.Add(created);
            return created;
        }

        if (directVaultIds != null)
        {
            foreach (var vid in directVaultIds)
            {
                user.ConnectionGroups.Add(GetOrCreateVault(vid));
            }
        }

        if (viaGroupVaultIds != null && viaGroupVaultIds.Length > 0)
        {
            var group = new UserGroup { Id = Guid.NewGuid(), Name = $"grp-{user.Email}" };
            foreach (var vid in viaGroupVaultIds)
            {
                group.Vaults.Add(GetOrCreateVault(vid));
            }
            group.Members.Add(user);
            ctx.UserGroups.Add(group);
        }

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetCurrentProfileAsync_ReturnsNull_ForUnknownPrincipal()
    {
        await using var ctx = NewContext();
        var svc = new AccessControlService(ctx);

        var profile = await svc.GetCurrentProfileAsync(PrincipalFor(Guid.NewGuid()));

        Assert.Null(profile);
    }

    [Fact]
    public async Task GetCurrentProfileAsync_ReturnsNull_WhenNameIdentifierMissing()
    {
        await using var ctx = NewContext();
        var svc = new AccessControlService(ctx);

        var profile = await svc.GetCurrentProfileAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Null(profile);
    }

    [Fact]
    public async Task GetCurrentProfileAsync_MergesDirectAndGroupVaults_Distinct()
    {
        await using var ctx = NewContext();
        var sharedVault = Guid.NewGuid();
        var directOnly = Guid.NewGuid();
        var groupOnly = Guid.NewGuid();

        var user = await SeedUserAsync(
            ctx,
            "alice@example.com",
            new[] { AppRoles.User },
            directVaultIds: new[] { directOnly, sharedVault },
            viaGroupVaultIds: new[] { groupOnly, sharedVault });

        var svc = new AccessControlService(ctx);
        var profile = await svc.GetCurrentProfileAsync(PrincipalFor(user.Id));

        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.UserId);
        Assert.Equal("alice@example.com", profile.Email);
        Assert.Contains(AppRoles.User, profile.Roles);
        Assert.Equal(3, profile.VaultIds.Count);
        Assert.Contains(sharedVault, profile.VaultIds);
        Assert.Contains(directOnly, profile.VaultIds);
        Assert.Contains(groupOnly, profile.VaultIds);
    }

    [Fact]
    public async Task GetCurrentProfileAsync_OwnerOrAdminFlags_Set()
    {
        await using var ctx = NewContext();
        var owner = await SeedUserAsync(ctx, "owner@x.com", new[] { AppRoles.Owner });
        var admin = await SeedUserAsync(ctx, "admin@x.com", new[] { AppRoles.Admin });
        var team = await SeedUserAsync(ctx, "team@x.com", new[] { AppRoles.TeamAdmin });
        var user = await SeedUserAsync(ctx, "user@x.com", new[] { AppRoles.User });

        var svc = new AccessControlService(ctx);

        var op = await svc.GetCurrentProfileAsync(PrincipalFor(owner.Id));
        var ap = await svc.GetCurrentProfileAsync(PrincipalFor(admin.Id));
        var tp = await svc.GetCurrentProfileAsync(PrincipalFor(team.Id));
        var up = await svc.GetCurrentProfileAsync(PrincipalFor(user.Id));

        Assert.True(op!.IsOwner);
        Assert.True(op.IsOwnerOrAdmin);
        Assert.False(op.IsTeamAdmin);

        Assert.True(ap!.IsAdmin);
        Assert.True(ap.IsOwnerOrAdmin);

        Assert.True(tp!.IsTeamAdmin);
        Assert.False(tp.IsOwnerOrAdmin);

        Assert.True(up!.IsDefaultUser);
        Assert.False(up.IsOwnerOrAdmin);
        Assert.False(up.IsTeamAdmin);
    }

    [Fact]
    public async Task ApplyConnectionScope_OwnerOrAdmin_ReturnsAll()
    {
        await using var ctx = NewContext();
        var vaultId = Guid.NewGuid();
        ctx.ConnectionGroups.Add(new ConnectionGroup { Id = vaultId, Name = "v1" });
        ctx.Connections.Add(new Connection { Id = Guid.NewGuid(), Name = "c1", HostId = Guid.NewGuid(), ConnectionGroupId = vaultId });
        ctx.Connections.Add(new Connection { Id = Guid.NewGuid(), Name = "c2", HostId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        var svc = new AccessControlService(ctx);
        var profile = new AccessProfile { UserId = Guid.NewGuid(), Roles = new[] { AppRoles.Admin } };

        var scoped = svc.ApplyConnectionScope(ctx.Connections, profile);
        Assert.Equal(2, await scoped.CountAsync());
    }

    [Fact]
    public async Task ApplyConnectionScope_RegularUser_FiltersByVaultOrDirectAssignment()
    {
        await using var ctx = NewContext();
        var allowedVault = Guid.NewGuid();
        var foreignVault = Guid.NewGuid();
        var directConn = Guid.NewGuid();
        var vaultConn = Guid.NewGuid();
        var hiddenConn = Guid.NewGuid();

        var user = await SeedUserAsync(ctx, "u@x.com", new[] { AppRoles.User }, directVaultIds: new[] { allowedVault });

        ctx.ConnectionGroups.Add(new ConnectionGroup { Id = foreignVault, Name = "foreign" });

        var inAllowedVault = new Connection { Id = vaultConn, Name = "vc", HostId = Guid.NewGuid(), ConnectionGroupId = allowedVault };
        var inForeignVault = new Connection { Id = hiddenConn, Name = "hc", HostId = Guid.NewGuid(), ConnectionGroupId = foreignVault };
        var directlyAssigned = new Connection { Id = directConn, Name = "dc", HostId = Guid.NewGuid(), ConnectionGroupId = foreignVault };
        directlyAssigned.Users.Add(await ctx.Users.FirstAsync(u => u.Id == user.Id));

        ctx.Connections.AddRange(inAllowedVault, inForeignVault, directlyAssigned);
        await ctx.SaveChangesAsync();

        var svc = new AccessControlService(ctx);
        var profile = (await svc.GetCurrentProfileAsync(PrincipalFor(user.Id)))!;

        var visible = await svc.ApplyConnectionScope(ctx.Connections, profile).Select(c => c.Id).ToListAsync();

        Assert.Contains(vaultConn, visible);
        Assert.Contains(directConn, visible);
        Assert.DoesNotContain(hiddenConn, visible);
    }

    [Fact]
    public async Task ApplyVaultScope_RegularUser_OnlyAccessibleVaults()
    {
        await using var ctx = NewContext();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();
        var v3 = Guid.NewGuid();
        ctx.ConnectionGroups.AddRange(
            new ConnectionGroup { Id = v1, Name = "v1" },
            new ConnectionGroup { Id = v2, Name = "v2" },
            new ConnectionGroup { Id = v3, Name = "v3" });
        await ctx.SaveChangesAsync();

        var svc = new AccessControlService(ctx);
        var profile = new AccessProfile { Roles = new[] { AppRoles.User }, VaultIds = new[] { v1, v3 } };

        var ids = await svc.ApplyVaultScope(ctx.ConnectionGroups, profile).Select(g => g.Id).ToListAsync();

        Assert.Equal(2, ids.Count);
        Assert.Contains(v1, ids);
        Assert.Contains(v3, ids);
    }

    [Fact]
    public async Task ApplyVaultScope_RegularUserWithoutVaults_ReturnsEmpty()
    {
        await using var ctx = NewContext();
        ctx.ConnectionGroups.Add(new ConnectionGroup { Id = Guid.NewGuid(), Name = "v" });
        await ctx.SaveChangesAsync();

        var svc = new AccessControlService(ctx);
        var profile = new AccessProfile { Roles = new[] { AppRoles.User } };

        Assert.Empty(await svc.ApplyVaultScope(ctx.ConnectionGroups, profile).ToListAsync());
    }

    [Fact]
    public void CanManageConnectionsInVault_OwnerOrAdmin_AlwaysTrue()
    {
        using var ctx = NewContext();
        var svc = new AccessControlService(ctx);

        var owner = new AccessProfile { Roles = new[] { AppRoles.Owner } };
        var admin = new AccessProfile { Roles = new[] { AppRoles.Admin } };

        Assert.True(svc.CanManageConnectionsInVault(owner, Guid.NewGuid()));
        Assert.True(svc.CanManageConnectionsInVault(owner, null));
        Assert.True(svc.CanManageConnectionsInVault(admin, Guid.NewGuid()));
    }

    [Fact]
    public void CanManageConnectionsInVault_TeamAdmin_OnlyForAssignedVaults()
    {
        using var ctx = NewContext();
        var svc = new AccessControlService(ctx);
        var assigned = Guid.NewGuid();
        var profile = new AccessProfile { Roles = new[] { AppRoles.TeamAdmin }, VaultIds = new[] { assigned } };

        Assert.True(svc.CanManageConnectionsInVault(profile, assigned));
        Assert.False(svc.CanManageConnectionsInVault(profile, Guid.NewGuid()));
        Assert.False(svc.CanManageConnectionsInVault(profile, null));
    }

    [Fact]
    public void CanManageConnectionsInVault_RegularUser_AlwaysFalse()
    {
        using var ctx = NewContext();
        var svc = new AccessControlService(ctx);
        var vaultId = Guid.NewGuid();
        var profile = new AccessProfile { Roles = new[] { AppRoles.User }, VaultIds = new[] { vaultId } };

        Assert.False(svc.CanManageConnectionsInVault(profile, vaultId));
        Assert.False(svc.CanManageConnectionsInVault(profile, Guid.NewGuid()));
    }

    [Fact]
    public void CanUseConnection_OwnerOrAdmin_AlwaysTrue()
    {
        using var ctx = NewContext();
        var svc = new AccessControlService(ctx);
        var profile = new AccessProfile { Roles = new[] { AppRoles.Admin } };
        var conn = new Connection { Id = Guid.NewGuid(), HostId = Guid.NewGuid() };

        Assert.True(svc.CanUseConnection(profile, conn));
    }

    [Fact]
    public void CanUseConnection_RegularUser_AllowedViaVaultOrDirectAssignment()
    {
        using var ctx = NewContext();
        var svc = new AccessControlService(ctx);
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var profile = new AccessProfile { UserId = userId, Roles = new[] { AppRoles.User }, VaultIds = new[] { vaultId } };

        var viaVault = new Connection { Id = Guid.NewGuid(), HostId = Guid.NewGuid(), ConnectionGroupId = vaultId };
        var viaDirect = new Connection { Id = Guid.NewGuid(), HostId = Guid.NewGuid(), ConnectionGroupId = Guid.NewGuid() };
        viaDirect.Users.Add(new User { Id = userId, Email = "x@x" });
        var hidden = new Connection { Id = Guid.NewGuid(), HostId = Guid.NewGuid(), ConnectionGroupId = Guid.NewGuid() };

        Assert.True(svc.CanUseConnection(profile, viaVault));
        Assert.True(svc.CanUseConnection(profile, viaDirect));
        Assert.False(svc.CanUseConnection(profile, hidden));
    }
}
