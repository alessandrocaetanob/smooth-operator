using System;
using System.Collections.Generic;

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
        public ICollection<User> Users { get; set; } = new List<User>();

        // Tags for filtering/categorising connections
        public ICollection<ConnectionTag> Tags { get; set; } = new List<ConnectionTag>();

        // JSON string to store arbitrary protocol-specific settings (like domain, security mode, ignore-cert, etc.)
        public string Settings { get; set; } = "{}";
    }
}
