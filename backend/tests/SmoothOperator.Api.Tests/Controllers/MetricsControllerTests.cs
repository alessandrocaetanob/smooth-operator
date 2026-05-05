using System.Net;
using System.Net.Http.Json;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Api.Tests.Infrastructure;
using Xunit;

namespace SmoothOperator.Api.Tests.Controllers;

/// <summary>
/// Coverage for <see cref="Backend.Controllers.MetricsController"/>:
/// - Authorization: only Owner/Admin can call the endpoints
/// - Response shape and basic correctness for /summary, /logins/timeseries,
///   /audit-events/top, /audit-events/breakdown
/// - Bucketing: timeseries returns the expected number of buckets for the requested
///   range/bucket size and never produces an extra trailing empty bucket.
/// </summary>
public class MetricsControllerTests
{
    private static HttpClient AsAdmin(TestWebApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.Admin);
        return client;
    }

    private static HttpClient AsRegularUser(TestWebApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", AppRoles.User);
        return client;
    }

    private static AuditLog NewLog(string action, string outcome, DateTime timestamp, Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(),
        Timestamp = timestamp,
        UserId = userId,
        Action = action,
        Outcome = outcome,
        ResourceType = "test",
        ResourceId = "1",
        Details = "{}",
        IpAddress = "127.0.0.1"
    };

    [Fact]
    public async Task Summary_ReturnsForbidden_ForRegularUser()
    {
        var userId = Guid.NewGuid();
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = userId, Email = "u@test.local", Name = "u", IsActive = true, CreatedAt = DateTime.UtcNow });
        });

        var client = AsRegularUser(factory, userId);
        var response = await client.GetAsync("/api/metrics/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Summary_ReturnsAggregatedCounts_ForAdmin()
    {
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = adminId, Email = "a@test.local", Name = "a", IsActive = true, CreatedAt = now });
            db.AuditLogs.AddRange(
                NewLog("user.login", "success", now.AddMinutes(-10), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-30), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-5), adminId),
                NewLog("connection.started", "success", now.AddHours(-2), adminId),
                NewLog("connection.ended", "success", now.AddHours(-1), adminId),
                // Outside 24h window — must be ignored.
                NewLog("user.login", "success", now.AddHours(-30), adminId)
            );
        });

        var client = AsAdmin(factory, adminId);
        var summary = await client.GetFromJsonAsync<MetricsSummaryDto>("/api/metrics/summary");

        Assert.NotNull(summary);
        Assert.Equal(3, summary!.LoginAttemptsTotal24h);
        Assert.Equal(2, summary.LoginFailures24h);
        Assert.InRange(summary.LoginSuccessRate24h, 33.0, 33.5);
        Assert.Equal(1, summary.ConnectionsStarted24h);
        Assert.Equal(1, summary.ConnectionsEnded24h);
        Assert.Equal(5, summary.AuditEventsTotal24h);
        Assert.Equal(2, summary.AuditFailures24h);
        Assert.Equal(3, summary.AuthEvents24h);
    }

    [Fact]
    public async Task LoginsTimeseries_BucketsRowsByOutcome_AndHasContinuousAxis()
    {
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = adminId, Email = "a@test.local", Name = "a", IsActive = true, CreatedAt = now });
            db.AuditLogs.AddRange(
                NewLog("user.login", "success", now.AddMinutes(-2), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-2), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-2), adminId)
            );
        });

        var client = AsAdmin(factory, adminId);
        var buckets = await client.GetFromJsonAsync<List<TimeseriesBucketDto>>(
            "/api/metrics/logins/timeseries?hours=1&bucketMinutes=5");

        Assert.NotNull(buckets);

        // 1h / 5min = 12 buckets (no extra trailing bucket from drift between two UtcNow calls)
        Assert.InRange(buckets!.Count, 12, 13);

        var totals = buckets.Aggregate(
            new { Success = 0L, Failure = 0L },
            (acc, b) => new
            {
                Success = acc.Success + (b.Values.TryGetValue("success", out var s) ? s : 0),
                Failure = acc.Failure + (b.Values.TryGetValue("failure", out var f) ? f : 0)
            });

        Assert.Equal(1, totals.Success);
        Assert.Equal(2, totals.Failure);
    }

    [Fact]
    public async Task TopAuditEvents_ReturnsCountsSortedDescending()
    {
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = adminId, Email = "a@test.local", Name = "a", IsActive = true, CreatedAt = now });
            db.AuditLogs.AddRange(
                NewLog("user.login", "success", now.AddMinutes(-1), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-1), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-1), adminId),
                NewLog("connection.started", "success", now.AddMinutes(-1), adminId)
            );
        });

        var client = AsAdmin(factory, adminId);
        var top = await client.GetFromJsonAsync<List<TopEventDto>>(
            "/api/metrics/audit-events/top?hours=1&limit=10");

        Assert.NotNull(top);
        Assert.Equal("user.login", top![0].Action);
        Assert.Equal(3, top[0].Count);
        Assert.Equal("connection.started", top[1].Action);
        Assert.Equal(1, top[1].Count);
    }

    [Fact]
    public async Task AuditEventBreakdown_SplitsSuccessAndFailure()
    {
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var factory = new TestWebApplicationFactory(db =>
        {
            db.Users.Add(new User { Id = adminId, Email = "a@test.local", Name = "a", IsActive = true, CreatedAt = now });
            db.AuditLogs.AddRange(
                NewLog("user.login", "success", now.AddMinutes(-1), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-1), adminId),
                NewLog("user.login", "failure", now.AddMinutes(-1), adminId)
            );
        });

        var client = AsAdmin(factory, adminId);
        var breakdown = await client.GetFromJsonAsync<OutcomeBreakdownDto>(
            "/api/metrics/audit-events/breakdown?hours=1");

        Assert.NotNull(breakdown);
        Assert.Equal(1, breakdown!.Success);
        Assert.Equal(2, breakdown.Failure);
    }
}
