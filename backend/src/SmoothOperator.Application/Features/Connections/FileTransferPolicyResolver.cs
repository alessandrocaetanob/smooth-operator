using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Connections
{
    /// <summary>
    /// Resolves the effective in-session file-transfer policy for a connection: the
    /// per-connection override when set, else the parent vault's default, else Disabled.
    /// Shared between the Connections DTO projection (so the frontend can gate the
    /// file-transfer UI without re-deriving the inherit rule) and the Guacamole session
    /// handshake (which uses the same resolution to decide guacd's connect parameters).
    /// </summary>
    public static class FileTransferPolicyResolver
    {
        public static FileTransferPolicy Resolve(Connection connection) =>
            connection.FileTransferPolicyOverride
            ?? connection.ConnectionGroup?.FileTransferPolicy
            ?? FileTransferPolicy.Disabled;
    }
}
