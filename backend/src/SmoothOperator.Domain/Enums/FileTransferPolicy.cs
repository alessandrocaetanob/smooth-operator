namespace SmoothOperator.Domain.Enums
{
    /// <summary>
    /// Governs in-session file transfer (SFTP for SSH, drive redirect for RDP).
    /// Used both as the vault-level default (<c>ConnectionGroup.FileTransferPolicy</c>)
    /// and, when set, as the per-connection override (<c>Connection.FileTransferPolicyOverride</c>,
    /// where <c>null</c> means "inherit the vault default").
    /// </summary>
    public enum FileTransferPolicy
    {
        Disabled = 0,
        DownloadOnly = 1,
        UploadOnly = 2,
        Both = 3,
    }
}
