using System.IO;

namespace SmoothOperator.Application.Options;

/// <summary>
/// In-session file-transfer paths bound from configuration / environment.
/// <c>FileTransfer__DrivePath</c> is the base directory guacd redirects as an RDP
/// network drive; a fresh per-session subdirectory is created under it for each
/// RDP session with file transfer enabled, and removed when the session ends.
///
/// SSH file transfer (SFTP) needs no local directory at all — guacd opens an SFTP
/// channel directly against the target SSH host using the connection's own
/// credentials, so only RDP needs this shared volume.
///
/// Defaults land in the OS temp directory so bare-metal dev installs work out of
/// the box. Docker compose overrides this to the in-container shared volume path.
/// </summary>
public sealed class FileTransferOptions
{
    public const string SectionName = "FileTransfer";

    /// <summary>
    /// Base directory shared with the guacd container. Each RDP session with file
    /// transfer enabled gets its own subdirectory here (named after the session id),
    /// redirected into the remote desktop as a network drive. Docker compose
    /// overrides this to <c>/var/file-transfer-temp</c>.
    /// </summary>
    public string DrivePath { get; set; } =
        Path.Combine(Path.GetTempPath(), "smooth-operator", "file-transfer-drive");
}
