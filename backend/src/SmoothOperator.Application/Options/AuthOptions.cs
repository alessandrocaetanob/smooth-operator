namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed authentication feature flags.
/// Bound from the "Auth" section in appsettings / environment variables (Auth__AllowSelfRegister).
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When true, the /register endpoint is open. Defaults to false (invite-only).</summary>
    public bool AllowSelfRegister { get; set; } = false;
}
