using System;
using System.Collections.Generic;

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
    }
}
