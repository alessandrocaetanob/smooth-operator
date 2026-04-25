using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class ConnectionGroup
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentGroupId { get; set; }

        // Navigation properties
        public ConnectionGroup? ParentGroup { get; set; }
        public ICollection<ConnectionGroup> SubGroups { get; set; } = new List<ConnectionGroup>();
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
    }
}
