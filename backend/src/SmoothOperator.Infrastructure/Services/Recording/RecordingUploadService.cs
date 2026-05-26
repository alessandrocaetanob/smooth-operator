using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services.Recording
{
    /// <summary>
    /// Reads the temp <c>.guac</c> file written by guacd, uploads it to the configured
    /// storage backend, deletes the temp file, and updates the <c>Recording</c> row.
    /// Failures are persisted as <see cref="RecordingStatus.Failed"/> with the error
    /// surfaced for admins; the retention sweep will retry deletion of the temp file.
    /// </summary>
    public sealed class RecordingUploadService : IRecordingUploadService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRecordingStorageFactory _storageFactory;
        private readonly ILogger<RecordingUploadService> _logger;

        public RecordingUploadService(
            IServiceScopeFactory scopeFactory,
            IRecordingStorageFactory storageFactory,
            ILogger<RecordingUploadService> logger)
        {
            _scopeFactory = scopeFactory;
            _storageFactory = storageFactory;
            _logger = logger;
        }

        public async Task UploadAndFinaliseAsync(Guid recordingId, string localFilePath, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var recording = await db.Recordings.FirstOrDefaultAsync(r => r.Id == recordingId, cancellationToken);
            if (recording == null)
            {
                _logger.LogWarning("Recording {RecordingId} not found; ignoring upload for {LocalFilePath}", recordingId, localFilePath);
                return;
            }

            if (!File.Exists(localFilePath))
            {
                recording.Status = RecordingStatus.Failed;
                recording.ErrorMessage = $"Temp recording file not found at '{localFilePath}'";
                recording.EndedAt ??= DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await audit.WriteAsync("recording.upload_failed", "Recording", recording.Id.ToString(),
                    new { recording.SessionId, error = recording.ErrorMessage }, outcome: "failure");
                return;
            }

            recording.Status = RecordingStatus.Uploading;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var storage = await _storageFactory.CreateAsync(cancellationToken);
                var result = await storage.UploadAsync(localFilePath, recording.StorageKey, cancellationToken);

                recording.Status = RecordingStatus.Available;
                recording.StorageType = storage.StorageType;
                recording.FileSizeBytes = result.SizeBytes;
                recording.EndedAt ??= DateTime.UtcNow;
                if (recording.EndedAt.HasValue)
                    recording.DurationSeconds = (int)Math.Max(0, (recording.EndedAt.Value - recording.StartedAt).TotalSeconds);
                await db.SaveChangesAsync(cancellationToken);

                await audit.WriteAsync("recording.uploaded", "Recording", recording.Id.ToString(), new
                {
                    recording.SessionId,
                    recording.ConnectionId,
                    storageType = storage.StorageType.ToString(),
                    recording.StorageKey,
                    recording.FileSizeBytes,
                    recording.DurationSeconds,
                });

                TryDeleteTempFile(localFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload recording {RecordingId} from {LocalFilePath}", recordingId, localFilePath);
                recording.Status = RecordingStatus.Failed;
                recording.ErrorMessage = ex.Message;
                recording.EndedAt ??= DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                await audit.WriteAsync("recording.upload_failed", "Recording", recording.Id.ToString(),
                    new { recording.SessionId, error = ex.Message }, outcome: "failure");
                // Leave the temp file alone — admin or retention sweep can deal with it.
            }
        }

        private void TryDeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp recording file {Path} after successful upload", path);
            }
        }
    }
}
