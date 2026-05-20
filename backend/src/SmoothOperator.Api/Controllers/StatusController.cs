using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmoothOperator.Api.Controllers;

/// <summary>
/// Public status endpoint for uptime monitors, status pages, and at-a-glance
/// dependency checks. Reports build version, uptime, and the readiness state
/// of tagged health checks. Intentionally outside the "auth" rate-limit policy.
/// </summary>
[ApiController]
[Route("status.json")]
[AllowAnonymous]
public class StatusController : ControllerBase
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] HealthCheckService hc,
        CancellationToken cancellationToken)
    {
        var report = await hc.CheckHealthAsync(c => c.Tags.Contains("ready"), cancellationToken);
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var commit = informational?.Split('+').ElementAtOrDefault(1) ?? "unknown";

        return Ok(new
        {
            version,
            commit,
            uptimeSeconds = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds,
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString().ToLowerInvariant() }),
        });
    }
}
