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
        var commit = ParseCommitHash(assembly);

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

    /// <summary>
    /// Extracts the git SHA injected at build time via the MSBuild
    /// <c>SourceRevisionId</c> property (which generates the
    /// <c>AssemblyInformationalVersionAttribute</c> value <c>X.Y.Z+&lt;sha&gt;</c>).
    /// Returns <c>"unknown"</c> when the attribute is missing or malformed.
    /// </summary>
    internal static string ParseCommitHash(System.Reflection.Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational)) return "unknown";

        var plusIdx = informational.IndexOf('+');
        if (plusIdx < 0 || plusIdx == informational.Length - 1) return "unknown";

        var sha = informational[(plusIdx + 1)..];
        // SourceRevisionId is a hex git SHA; reject anything else as malformed.
        foreach (var c in sha)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return "unknown";
        }
        return sha;
    }
}
