using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed configuration for outbound webhook delivery.
/// Bound from the "Webhooks" section (env vars Webhooks__PollIntervalSeconds, etc.).
/// </summary>
public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>How often the background worker scans for due deliveries.</summary>
    [Range(1, 3600)]
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>Maximum deliveries drained per scan.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    /// <summary>Parallel in-flight HTTP requests when draining a batch.</summary>
    [Range(1, 200)]
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>Total attempts before a delivery is marked Dead.</summary>
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base for exponential backoff: retry delay = RetryBaseSeconds * 2^(attempt-1).</summary>
    [Range(1, 3600)]
    public int RetryBaseSeconds { get; set; } = 60;

    /// <summary>Per-request HTTP timeout for a delivery POST.</summary>
    [Range(1, 300)]
    public int HttpTimeoutSeconds { get; set; } = 15;

    /// <summary>Delivered/Dead rows older than this are purged by the worker.</summary>
    [Range(1, 3650)]
    public int DeliveryRetentionDays { get; set; } = 30;
}
