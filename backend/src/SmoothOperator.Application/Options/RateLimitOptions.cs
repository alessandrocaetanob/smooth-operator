using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed rate-limiting configuration.
/// Bound from the "RateLimit" section in appsettings / environment variables.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public GlobalRateLimitOptions Global { get; set; } = new();

    public AuthRateLimitOptions Auth { get; set; } = new();
}

public sealed class GlobalRateLimitOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 100;

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; } = 0;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;
}

public sealed class AuthRateLimitOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 5;

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; } = 0;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;
}
