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
    }
}
