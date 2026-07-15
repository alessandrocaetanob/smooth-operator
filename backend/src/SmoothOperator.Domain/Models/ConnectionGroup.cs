using System;
using System.Collections.Generic;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Domain.Models
{
    public class ConnectionGroup
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentGroupId { get; set; }

        // Navigation properties
        public ConnectionGroup? ParentGroup { get; set; }
        public ICollection<ConnectionGroup> SubGroups { get; set; } = [];
        public ICollection<Connection> Connections { get; set; } = [];
        public ICollection<User> Users { get; set; } = [];
        public ICollection<UserGroup> Groups { get; set; } = [];

        /// <summary>Default recording-on flag for connections in this vault. Individual connections can override via <c>Connection.RecordingOverride</c>.</summary>
        public bool RecordingEnabled { get; set; }

        /// <summary>Whether recordings should include keystrokes (passwords typed at prompts!). Defaults to false; admins must opt-in per vault.</summary>
        public bool RecordingIncludeKeys { get; set; }

        /// <summary>Optional per-vault retention. <c>null</c> defers to <c>RecordingStorageSettings.RetentionDays</c>.</summary>
        public int? RecordingRetentionDays { get; set; }

        /// <summary>Default in-session file-transfer policy for connections in this vault. Individual connections can override via <c>Connection.FileTransferPolicyOverride</c>.</summary>
        public FileTransferPolicy FileTransferPolicy { get; set; } = FileTransferPolicy.Disabled;

        /// <summary>
        /// Inactivity timeout, in seconds, for in-session file-transfer operations
        /// (directory listing, upload, download) on connections in this vault. The
        /// client aborts an operation with a visible error when no data flows for
        /// this long. Null uses the application default.
        /// </summary>
        public int? FileTransferTimeoutSeconds { get; set; }
    }
}
