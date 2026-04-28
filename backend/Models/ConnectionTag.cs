using System;

namespace Backend.Models
{
    public class ConnectionTag
    {
        public int Id { get; set; }
        public Guid ConnectionId { get; set; }
        public Connection Connection { get; set; } = null!;

        public string Tag { get; set; } = string.Empty;
    }
}
