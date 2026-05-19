using System;
using System.Collections.Generic;

namespace SmoothOperator.Domain.Models
{
    public class Host
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty; // IP or Hostname

        // Navigation properties
        public ICollection<Connection> Connections { get; set; } = [];
    }
}
