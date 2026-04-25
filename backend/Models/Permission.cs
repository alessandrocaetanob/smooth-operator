using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class Permission
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., "connections.read", "hosts.write"

        // Navigation properties
        public ICollection<Role> Roles { get; set; } = new List<Role>();
    }
}
