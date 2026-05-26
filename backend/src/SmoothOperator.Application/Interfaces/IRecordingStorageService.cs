using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// Pluggable destination for captured <c>.guac</c> session recordings.
    /// One implementation per <see cref="RecordingStorageType"/>; resolved at runtime
    /// from <c>RecordingStorageSettings</c> via <see cref="IRecordingStorageFactory"/>.
    /// </summary>
    public interface IRecordingStorageService
    {
        RecordingStorageType StorageType { get; }

        /// <summary>
        /// Persists a local file to the backend and returns the long-term storage key + size.
        /// Implementations must be idempotent on the key (re-uploading the same key replaces).
        /// </summary>
        Task<RecordingUploadResult> UploadAsync(string localFilePath, string storageKey, CancellationToken cancellationToken);

        Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

        Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

        /// <summary>
        /// Cheap health-check used by the settings page's "Test connection" button.
        /// Returns the surfaced error message on failure, or <c>null</c> on success.
        /// </summary>
        Task<string?> TestConnectionAsync(CancellationToken cancellationToken);
    }

    public sealed record RecordingUploadResult(string StorageKey, long SizeBytes);
}
