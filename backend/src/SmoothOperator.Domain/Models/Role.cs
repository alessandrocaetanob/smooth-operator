using System;
using System.Collections.Generic;

namespace SmoothOperator.Domain.Models
{
    public class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<User> Users { get; set; } = [];
        public ICollection<Permission> Permissions { get; set; } = [];
    }
}
