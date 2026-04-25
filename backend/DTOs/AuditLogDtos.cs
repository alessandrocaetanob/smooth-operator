using System;
using System.Collections.Generic;

namespace Backend.DTOs
{
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
    }
}
