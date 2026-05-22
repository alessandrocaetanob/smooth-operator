using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GuacamoleProxyService.EvaluateSessionTimeout"/> — the
/// pure decision function pulled out of the watchdog so it can be tested without
/// standing up a real guacd connection or WebSocket pair.
/// </summary>
public class GuacamoleSessionTimeoutTests
{
    private static readonly DateTime Start = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NullWhenBothTimeoutsDisabled()
    {
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start,
            now: Start.AddHours(5),
            idleTimeoutMinutes: 0,
            maxSessionMinutes: 0);
        Assert.Null(reason);
    }

    [Fact]
    public void IdleTimeoutTriggers_WhenInactivityExceedsThreshold()
    {
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start.AddMinutes(2),
            now: Start.AddMinutes(20),
            idleTimeoutMinutes: 15,
            maxSessionMinutes: 0);
        Assert.Equal("idle-timeout", reason);
    }

    [Fact]
    public void IdleTimeoutDoesNotTrigger_WhenWithinThreshold()
    {
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start.AddMinutes(14),
            now: Start.AddMinutes(20),
            idleTimeoutMinutes: 15,
            maxSessionMinutes: 0);
        Assert.Null(reason);
    }

    [Fact]
    public void MaxSessionTriggers_WhenTotalRuntimeExceedsCap()
    {
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start.AddMinutes(100),
            now: Start.AddMinutes(125),
            idleTimeoutMinutes: 0,
            maxSessionMinutes: 120);
        Assert.Equal("max-session-exceeded", reason);
    }

    [Fact]
    public void MaxSessionDoesNotTrigger_WhenWithinCap()
    {
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start.AddMinutes(60),
            now: Start.AddMinutes(60),
            idleTimeoutMinutes: 0,
            maxSessionMinutes: 120);
        Assert.Null(reason);
    }

    [Fact]
    public void IdlePrecedesMax_WhenBothWouldFire()
    {
        // When both limits are violated, idle wins so the audit reflects the
        // proximate cause (user walked away) rather than the absolute cap.
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start,
            now: Start.AddHours(3),
            idleTimeoutMinutes: 15,
            maxSessionMinutes: 60);
        Assert.Equal("idle-timeout", reason);
    }

    [Fact]
    public void OnlyIdleConfigured_MaxDoesNotFire()
    {
        var reason = GuacamoleProxyService.EvaluateSessionTimeout(
            sessionStartedAt: Start,
            lastActivityAt: Start.AddMinutes(1),
            now: Start.AddDays(1),
            idleTimeoutMinutes: 0,
            maxSessionMinutes: 0);
        Assert.Null(reason);
    }
}
