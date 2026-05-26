using System.IO;

namespace SmoothOperator.Application.Options;

/// <summary>
/// Recording paths bound from configuration / environment.
/// <c>Recording__LocalPath</c> is where guacd writes the in-progress <c>.guac</c> file;
/// <c>Recording__DefaultStoragePath</c> is the default destination for the Local storage backend
/// (admins can override via the settings page; this is just the fallback when nothing is configured).
///
/// Defaults land in the OS temp directory so bare-metal dev installs work out of the box.
/// Docker compose overrides both to the in-container <c>/var/recordings*</c> volume paths.
/// </summary>
public sealed class RecordingOptions
{
    public const string SectionName = "Recording";

    /// <summary>
    /// Temp directory shared between the backend and guacd containers. guacd writes the
    /// in-progress <c>.guac</c> stream here; the backend reads + uploads + deletes after
    /// session-end. Docker compose overrides this to <c>/var/recordings/temp</c>.
    /// </summary>
    public string LocalPath { get; set; } =
        Path.Combine(Path.GetTempPath(), "smooth-operator", "recordings-temp");

    /// <summary>
    /// Default destination directory for the Local storage backend.
    /// Overridable per deployment, then again per install via the settings page.
    /// Docker compose overrides this to <c>/var/recordings</c>.
    /// </summary>
    public string DefaultStoragePath { get; set; } =
        Path.Combine(Path.GetTempPath(), "smooth-operator", "recordings");
}
