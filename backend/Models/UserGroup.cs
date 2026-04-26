using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class UserGroup
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<User> Members { get; set; } = new List<User>();
        public ICollection<ConnectionGroup> Vaults { get; set; } = new List<ConnectionGroup>();
    }
}
