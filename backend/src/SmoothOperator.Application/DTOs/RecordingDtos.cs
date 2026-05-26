using System;
using System.Collections.Generic;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.DTOs
{
    public class RecordingDto
    {
        public Guid Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public Guid ConnectionId { get; set; }
        public string ConnectionName { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public Guid? VaultId { get; set; }
        public string? VaultName { get; set; }

        public Guid UserId { get; set; }
        public string? UserEmail { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }

        public RecordingStorageType StorageType { get; set; }
        public long? FileSizeBytes { get; set; }
        public RecordingStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IncludeKeys { get; set; }
    }

    public class RecordingsListDto
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<RecordingDto> Items { get; set; } = new();
    }
}
