using System;
using System.Collections.Generic;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Domain.Models
{
    public class Connection
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Protocol { get; set; } = "rdp"; // rdp, ssh, vnc

        // Foreign keys
        public Guid HostId { get; set; }
        public Guid? CredentialId { get; set; }
        public Guid? ConnectionGroupId { get; set; }

        // Navigation properties
        public Host? Host { get; set; }
        public Credential? Credential { get; set; }
        public ConnectionGroup? ConnectionGroup { get; set; }

        // M2M for direct assignments
        public ICollection<User> Users { get; set; } = [];

        // Tags for filtering/categorising connections
        public ICollection<ConnectionTag> Tags { get; set; } = [];

        // JSON string to store arbitrary protocol-specific settings (like domain, security mode, ignore-cert, etc.)
        public string Settings { get; set; } = "{}";

        /// <summary>
        /// Override the parent vault's recording flag. <c>Inherit</c> (default) defers to
        /// <see cref="ConnectionGroup.RecordingEnabled"/>; otherwise <c>ForceOn</c>/<c>ForceOff</c>
        /// wins regardless of the vault setting.
        /// </summary>
        public RecordingOverride RecordingOverride { get; set; } = RecordingOverride.Inherit;

        /// <summary>Per-connection include-keystrokes override; <c>null</c> = inherit from vault.</summary>
        public bool? RecordingIncludeKeys { get; set; }
    }
}
