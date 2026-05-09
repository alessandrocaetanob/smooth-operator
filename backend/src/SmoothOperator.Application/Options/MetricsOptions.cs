namespace SmoothOperator.Application.Options;

/// <summary>
/// Optional bearer-token protection for the /metrics (Prometheus scrape) endpoint.
/// When <see cref="BearerToken"/> is null or empty the endpoint is unauthenticated,
/// which is acceptable for local development but must NOT be used in production.
/// In production, set Metrics__BearerToken to a strong random secret and configure
/// the same value in your Prometheus scrape config (authorization.credentials).
/// </summary>
public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// When set, requests to /metrics must include "Authorization: Bearer {BearerToken}".
    /// Leave unset only in local development environments.
    /// </summary>
    public string? BearerToken { get; init; }
}
