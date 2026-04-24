using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? EntraObjectId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Role> Roles { get; set; } = new List<Role>();
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
    }
}
