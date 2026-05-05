namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed application URL configuration.
/// Bound from the "AppUrls" section in appsettings / environment variables (AppUrls__App, AppUrls__Frontend).
/// </summary>
public sealed class AppUrlsOptions
{
    public const string SectionName = "AppUrls";

    /// <summary>Base URL of this backend API (e.g. https://api.myorg.example).</summary>
    public string App { get; set; } = string.Empty;

    /// <summary>Base URL of the Angular frontend (e.g. https://myorg.example).</summary>
    public string Frontend { get; set; } = string.Empty;
}
