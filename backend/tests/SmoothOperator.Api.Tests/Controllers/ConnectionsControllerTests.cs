using System;
using System.Collections.Concurrent;
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

public class ConnectionsControllerTests
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

    [Fact]
    public async Task ProbeConnectionsBulk_ReturnsReachability()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connId1 = Guid.NewGuid();
        var connId2 = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);
            var host = new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" };
            db.Hosts.Add(host);

            db.Connections.Add(new Connection { Id = connId1, Name = "c1", Protocol = "ssh", HostId = hostId, ConnectionGroupId = vaultId, Settings = "{}" });
            db.Connections.Add(new Connection { Id = connId2, Name = "c2", Protocol = "rdp", HostId = hostId, ConnectionGroupId = vaultId, Settings = "{}" });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);

        var ids = new List<Guid> { connId1, connId2 };
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", ids);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        // Accessible connections with a host return "up" or "down" — never null or error statuses.
        Assert.True(results[connId1] == "up" || results[connId1] == "down",
            $"Expected 'up' or 'down' for connId1, got '{results[connId1]}'");
        Assert.True(results[connId2] == "up" || results[connId2] == "down",
            $"Expected 'up' or 'down' for connId2, got '{results[connId2]}'");
    }

    [Fact]
    public async Task ProbeConnectionsBulk_MissingId_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", new List<Guid> { missingId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal("not_found", results[missingId]);
    }

    [Fact]
    public async Task ProbeConnectionsBulk_ForbiddenId_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var ownVaultId = Guid.NewGuid();
        var otherVaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var forbiddenConnId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var ownVault = new ConnectionGroup { Id = ownVaultId, Name = "mine" };
            var otherVault = new ConnectionGroup { Id = otherVaultId, Name = "theirs" };
            db.ConnectionGroups.AddRange(ownVault, otherVault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "10.0.0.1" });
            // This connection belongs to a vault the user has no access to.
            db.Connections.Add(new Connection { Id = forbiddenConnId, Name = "secret", Protocol = "rdp", HostId = hostId, ConnectionGroupId = otherVaultId, Settings = "{}" });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(ownVault); // user only has access to ownVault
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", new List<Guid> { forbiddenConnId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal("forbidden", results[forbiddenConnId]);
    }

    [Fact]
    public async Task ProbeConnectionsBulk_HostlessConnection_ReturnsNoHost()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostlessConnId = Guid.NewGuid();
        // A host ID that is never inserted into the database — Host nav property will be null.
        var nonExistentHostId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var v = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(v);

            // Connection references a host that doesn't exist — Host navigation property will be null.
            db.Connections.Add(new Connection { Id = hostlessConnId, Name = "no-host", Protocol = "rdp", HostId = nonExistentHostId, ConnectionGroupId = vaultId, Settings = "{}" });

            var u = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, u, AppRoles.User);
            u.ConnectionGroups.Add(v);
            db.Users.Add(u);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var res = await client.PostAsJsonAsync("/api/connections/probe-bulk", new List<Guid> { hostlessConnId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
        Assert.NotNull(results);
        Assert.Equal("no_host", results[hostlessConnId]);
    }

    [Fact]
    public async Task GetMyLastConnected_ReturnsOrderedValidConnectionIds()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var latestConnectionId = Guid.NewGuid();
        var olderConnectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);

            var otherUser = new User { Id = otherUserId, Email = "other@x", Name = "other", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, otherUser, AppRoles.User);
            db.Users.Add(otherUser);

            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "connection.started",
                    ResourceType = "connection",
                    ResourceId = olderConnectionId.ToString(),
                    Timestamp = DateTime.UtcNow.AddMinutes(-10)
                },
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "connection.started",
                    ResourceType = "connection",
                    ResourceId = latestConnectionId.ToString(),
                    Timestamp = DateTime.UtcNow.AddMinutes(-1)
                },
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "connection.started",
                    ResourceType = "connection",
                    ResourceId = "not-a-guid",
                    Timestamp = DateTime.UtcNow
                },
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "connection.updated",
                    ResourceType = "connection",
                    ResourceId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow
                },
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = otherUserId,
                    Action = "connection.started",
                    ResourceType = "connection",
                    ResourceId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow
                });
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync("/api/connections/my-last-connected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await response.Content.ReadFromJsonAsync<List<Guid>>();
        Assert.NotNull(ids);
        Assert.Equal(2, ids.Count);
        Assert.Equal(latestConnectionId, ids[0]);
        Assert.Equal(olderConnectionId, ids[1]);
    }

    [Fact]
    public async Task GetMyLastConnected_WhenProfileMissing_ReturnsUnauthorized()
    {
        var missingUserId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory();

        var client = AsUser(factory, missingUserId, AppRoles.User);
        var response = await client.GetAsync("/api/connections/my-last-connected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProbeConnection_WhenProfileMissing_ReturnsForbidden()
    {
        var missingUserId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory();

        var client = AsUser(factory, missingUserId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{Guid.NewGuid()}/probe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Equal("forbidden", result);
    }

    [Fact]
    public async Task ProbeConnection_MissingConnection_ReturnsNotFoundStatus()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{Guid.NewGuid()}/probe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Equal("not_found", result);
    }

    [Fact]
    public async Task ProbeConnection_ForbiddenConnection_ReturnsForbiddenStatus()
    {
        var userId = Guid.NewGuid();
        var ownVaultId = Guid.NewGuid();
        var otherVaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var forbiddenConnId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var ownVault = new ConnectionGroup { Id = ownVaultId, Name = "mine" };
            var otherVault = new ConnectionGroup { Id = otherVaultId, Name = "theirs" };
            db.ConnectionGroups.AddRange(ownVault, otherVault);
            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "h", Address = "127.0.0.1" });
            db.Connections.Add(new Connection
            {
                Id = forbiddenConnId,
                Name = "secret",
                Protocol = "ssh",
                HostId = hostId,
                ConnectionGroupId = otherVaultId,
                Settings = "{}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(ownVault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{forbiddenConnId}/probe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Equal("forbidden", result);
    }

    [Fact]
    public async Task ProbeConnection_WithInvalidPortSetting_FallsBackToDefaultAndReturnsDown()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "v" };
            db.ConnectionGroups.Add(vault);
            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "invalid-address", Address = "256.256.256.256" });
            db.Connections.Add(new Connection
            {
                Id = connId,
                Name = "down-connection",
                Protocol = "telnet",
                HostId = hostId,
                ConnectionGroupId = vaultId,
                Settings = "{\"port\":\"not-an-int\"}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connId}/probe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Equal("down", result);
    }

    [Fact]
    public async Task DownloadConnectionFile_ReturnsRdpContent()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "Vault" };
            db.ConnectionGroups.Add(vault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host
            {
                Id = hostId,
                Name = "Prod Host",
                Address = "10.10.10.10"
            });

            db.Credentials.Add(new Credential
            {
                Id = credentialId,
                Name = "ServerCred",
                CredentialType = "password",
                Username = "administrator",
                EncryptedSecret = "encrypted"
            });

            db.Connections.Add(new Connection
            {
                Id = connectionId,
                Name = "Prod RDP",
                Protocol = "rdp",
                HostId = hostId,
                CredentialId = credentialId,
                ConnectionGroupId = vaultId,
                Settings = "{\"port\":\"3391\"}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connectionId}/file?format=rdp");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("application/x-rdp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Prod RDP.rdp", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("full address:s:10.10.10.10:3391", body);
        Assert.Contains("username:s:administrator", body);
    }

    [Fact]
    public async Task DownloadConnectionFile_WithUnsupportedFormat_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "Vault" };
            db.ConnectionGroups.Add(vault);
            db.Hosts.Add(new SmoothOperator.Domain.Models.Host { Id = hostId, Name = "Host", Address = "127.0.0.1" });
            db.Connections.Add(new Connection
            {
                Id = connectionId,
                Name = "Conn",
                Protocol = "rdp",
                HostId = hostId,
                ConnectionGroupId = vaultId,
                Settings = "{}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connectionId}/file?format=telnet");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DownloadSshFile_ReturnsValidShellScript()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "Vault" };
            db.ConnectionGroups.Add(vault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host
            {
                Id = hostId,
                Name = "SSH Host",
                Address = "ssh.example.com"
            });

            db.Credentials.Add(new Credential
            {
                Id = credentialId,
                Name = "SSHCred",
                CredentialType = "password",
                Username = "deploy",
                EncryptedSecret = "encrypted"
            });

            db.Connections.Add(new Connection
            {
                Id = connectionId,
                Name = "SSH Conn",
                Protocol = "ssh",
                HostId = hostId,
                CredentialId = credentialId,
                ConnectionGroupId = vaultId,
                Settings = "{\"port\":\"2222\"}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connectionId}/file?format=ssh");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-sh", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("#!/bin/bash", body);
        Assert.Contains("ssh -p 2222 'deploy'@'ssh.example.com'", body);
    }

    [Fact]
    public async Task DownloadSshFile_WithBracketedIpv6Host_ReturnsValidShellScript()
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "Vault" };
            db.ConnectionGroups.Add(vault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host
            {
                Id = hostId,
                Name = "IPv6 Host",
                Address = "[::1]"
            });

            db.Credentials.Add(new Credential
            {
                Id = credentialId,
                Name = "SSHCred",
                CredentialType = "password",
                Username = "deploy",
                EncryptedSecret = "encrypted"
            });

            db.Connections.Add(new Connection
            {
                Id = connectionId,
                Name = "SSH Conn IPv6",
                Protocol = "ssh",
                HostId = hostId,
                CredentialId = credentialId,
                ConnectionGroupId = vaultId,
                Settings = "{}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connectionId}/file?format=ssh");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ssh -p 22 'deploy'@'[::1]'", body);
    }

    [Theory]
    [InlineData("'; rm -rf /")]
    [InlineData("`whoami`")]
    [InlineData("$(evil)")]
    [InlineData("user\nmalicious")]
    [InlineData("user;id")]
    [InlineData("user|cat /etc/passwd")]
    [InlineData("user&background")]
    public async Task DownloadSshFile_WithMaliciousUsername_ReturnsBadRequest(string maliciousUsername)
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "Vault" };
            db.ConnectionGroups.Add(vault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host
            {
                Id = hostId,
                Name = "Host",
                Address = "safe-host.example.com"
            });

            db.Credentials.Add(new Credential
            {
                Id = credentialId,
                Name = "Cred",
                CredentialType = "password",
                Username = maliciousUsername,
                EncryptedSecret = "encrypted"
            });

            db.Connections.Add(new Connection
            {
                Id = connectionId,
                Name = "SSH Conn",
                Protocol = "ssh",
                HostId = hostId,
                CredentialId = credentialId,
                ConnectionGroupId = vaultId,
                Settings = "{}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connectionId}/file?format=ssh");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("'; rm -rf /")]
    [InlineData("host`whoami`")]
    [InlineData("$(evil).com")]
    [InlineData("host\nmalicious")]
    [InlineData("host;id")]
    [InlineData("host|ls")]
    [InlineData("a]")]
    [InlineData("[::1")]
    public async Task DownloadSshFile_WithMaliciousHost_ReturnsBadRequest(string maliciousHost)
    {
        var userId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using var factory = new TestWebApplicationFactory(db =>
        {
            var vault = new ConnectionGroup { Id = vaultId, Name = "Vault" };
            db.ConnectionGroups.Add(vault);

            db.Hosts.Add(new SmoothOperator.Domain.Models.Host
            {
                Id = hostId,
                Name = "Host",
                Address = maliciousHost
            });

            db.Connections.Add(new Connection
            {
                Id = connectionId,
                Name = "SSH Conn",
                Protocol = "ssh",
                HostId = hostId,
                ConnectionGroupId = vaultId,
                Settings = "{}"
            });

            var user = new User { Id = userId, Email = "u@x", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow };
            AttachRoles(db, user, AppRoles.User);
            user.ConnectionGroups.Add(vault);
            db.Users.Add(user);
        });

        var client = AsUser(factory, userId, AppRoles.User);
        var response = await client.GetAsync($"/api/connections/{connectionId}/file?format=ssh");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
