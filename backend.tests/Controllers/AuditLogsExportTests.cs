using System.Net;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Tests.Infrastructure;
using Xunit;

namespace Backend.Tests.Controllers;

public class AuditLogsExportTests
{
    private static HttpClient AsAdmin(TestWebApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.Admin);
        return client;
    }

    [Theory]
    [InlineData("=DANGEROUS()", "'=DANGEROUS()")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public async Task Export_FormulaTriggerChars_ArePrefixedWithSingleQuote(string payload, string expectedCsvValue)
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin@test.local", Name = "admin", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Users.Add(admin);
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = adminId,
                Action = "test.action",
                ResourceType = "test",
                ResourceId = "1",
                Details = payload,
                IpAddress = "127.0.0.1",
                Outcome = "success"
            });
        });

        var client = AsAdmin(factory, adminId);
        var response = await client.GetAsync("/api/audit-logs/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains(expectedCsvValue, csv);
    }

    [Theory]
    [InlineData(" =DANGEROUS()", "' =DANGEROUS()")]   // leading space
    [InlineData("\t=DANGEROUS()", "'\t=DANGEROUS()")]  // leading tab
    [InlineData("\r=DANGEROUS()", "'\r=DANGEROUS()")]  // leading CR
    [InlineData("\n=DANGEROUS()", "'\n=DANGEROUS()")]  // leading LF
    [InlineData("  \t\n=cmd", "'  \t\n=cmd")]          // mixed leading whitespace
    public async Task Export_LeadingWhitespaceThenFormulaTrigger_ArePrefixedWithSingleQuote(string payload, string expectedRawPrefix)
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin2@test.local", Name = "admin2", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Users.Add(admin);
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = adminId,
                Action = "test.action",
                ResourceType = "test",
                ResourceId = "1",
                Details = payload,
                IpAddress = "127.0.0.1",
                Outcome = "success"
            });
        });

        var client = AsAdmin(factory, adminId);
        var response = await client.GetAsync("/api/audit-logs/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();

        // The exported CSV must contain the single-quote-prefixed original value.
        Assert.Contains(expectedRawPrefix, csv);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("normal text")]
    [InlineData("100")]
    public async Task Export_SafeValues_AreNotPrefixed(string payload)
    {
        var adminId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            var admin = new User { Id = adminId, Email = "admin3@test.local", Name = "admin3", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Users.Add(admin);
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = adminId,
                Action = "test.action",
                ResourceType = "test",
                ResourceId = "1",
                Details = payload,
                IpAddress = "127.0.0.1",
                Outcome = "success"
            });
        });

        var client = AsAdmin(factory, adminId);
        var response = await client.GetAsync("/api/audit-logs/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains(payload, csv);
        Assert.DoesNotContain("'" + payload, csv);
    }
}
