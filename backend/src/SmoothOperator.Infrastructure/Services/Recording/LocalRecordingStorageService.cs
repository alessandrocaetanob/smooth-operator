using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Infrastructure.Services.Recording
{
    /// <summary>
    /// Persists recordings on the local filesystem under the configured base path.
    /// The base path is typically a docker volume so recordings survive container restarts.
    /// </summary>
    public sealed class LocalRecordingStorageService : IRecordingStorageService
    {
        private readonly string _basePath;

        public LocalRecordingStorageService(string basePath)
        {
            _basePath = basePath;
        }

        public RecordingStorageType StorageType => RecordingStorageType.Local;

        public async Task<RecordingUploadResult> UploadAsync(string localFilePath, string storageKey, CancellationToken cancellationToken)
        {
            var dest = ResolvePath(storageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            if (string.Equals(Path.GetFullPath(localFilePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            {
                // Source already lives at the final destination — nothing to copy.
                var existing = new FileInfo(dest).Length;
                return new RecordingUploadResult(storageKey, existing);
            }

            // Stream copy — recordings can be hundreds of MB for long RDP sessions.
            await using (var src = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            await using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await src.CopyToAsync(dst, cancellationToken);
            }

            var info = new FileInfo(dest);
            return new RecordingUploadResult(storageKey, info.Length);
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            var path = ResolvePath(storageKey);
            Stream s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult(s);
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            var path = ResolvePath(storageKey);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        public Task<string?> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(_basePath);
                var probe = Path.Combine(_basePath, ".write-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return Task.FromResult<string?>(null);
            }
            catch (Exception ex)
            {
                return Task.FromResult<string?>($"Local path '{_basePath}' is not writable: {ex.Message}");
            }
        }

        private string ResolvePath(string storageKey)
        {
            // Normalize forward-slash keys to platform separators; reject path traversal.
            if (storageKey.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException("Storage key must not contain '..'", nameof(storageKey));
            var relative = storageKey.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_basePath, relative);
        }
    }
}
