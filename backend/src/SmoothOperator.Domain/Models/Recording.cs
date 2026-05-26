using System;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// One captured session — a row per recording attempt, regardless of upload outcome.
    /// guacd writes a raw <c>.guac</c> protocol stream to a temp path, the backend uploads
    /// to the configured <see cref="RecordingStorageType"/> after session-end, then this
    /// row's <see cref="Status"/> moves Recording → Uploading → Available (or Failed).
    /// </summary>
    public class Recording
    {
        public Guid Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public Guid ConnectionId { get; set; }
        public Guid UserId { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }

        public string StorageKey { get; set; } = string.Empty;
        public RecordingStorageType StorageType { get; set; }
        public long? FileSizeBytes { get; set; }

        public bool IncludeKeys { get; set; }

        public RecordingStatus Status { get; set; } = RecordingStatus.Recording;
        public string? ErrorMessage { get; set; }

        public Connection? Connection { get; set; }
        public User? User { get; set; }
    }
}
