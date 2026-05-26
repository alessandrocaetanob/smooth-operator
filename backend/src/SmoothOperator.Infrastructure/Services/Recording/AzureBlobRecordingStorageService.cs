using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Infrastructure.Services.Recording
{
    /// <summary>
    /// Azure Blob Storage backend. Authenticates with the account name + shared key.
    /// </summary>
    public sealed class AzureBlobRecordingStorageService : IRecordingStorageService
    {
        private readonly BlobContainerClient _container;
        private readonly string? _prefix;

        public AzureBlobRecordingStorageService(AzureBlobStorageConfig config)
        {
            var serviceUri = string.IsNullOrWhiteSpace(config.BlobEndpoint)
                ? new Uri($"https://{config.AccountName}.blob.core.windows.net")
                : new Uri(config.BlobEndpoint);

            var credential = new StorageSharedKeyCredential(config.AccountName, config.AccountKey);
            var serviceClient = new BlobServiceClient(serviceUri, credential);
            _container = serviceClient.GetBlobContainerClient(config.ContainerName);
            _prefix = NormalisePrefix(config.PathPrefix);
        }

        public RecordingStorageType StorageType => RecordingStorageType.AzureBlob;

        public async Task<RecordingUploadResult> UploadAsync(string localFilePath, string storageKey, CancellationToken cancellationToken)
        {
            var blob = _container.GetBlobClient(ResolveKey(storageKey));
            await using (var src = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                await blob.UploadAsync(src, overwrite: true, cancellationToken);
            }
            var size = new FileInfo(localFilePath).Length;
            return new RecordingUploadResult(storageKey, size);
        }

        public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            var blob = _container.GetBlobClient(ResolveKey(storageKey));
            var resp = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return resp.Value.Content;
        }

        public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            var blob = _container.GetBlobClient(ResolveKey(storageKey));
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        }

        public async Task<string?> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                return null;
            }
            catch (RequestFailedException ex)
            {
                return $"Azure Blob: {ex.ErrorCode} — {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Azure Blob: {ex.Message}";
            }
        }

        private string ResolveKey(string storageKey)
        {
            if (storageKey.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException("Storage key must not contain '..'", nameof(storageKey));
            return string.IsNullOrEmpty(_prefix) ? storageKey : $"{_prefix}/{storageKey}";
        }

        private static string? NormalisePrefix(string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return null;
            return prefix.Trim().Trim('/');
        }
    }

    public sealed record AzureBlobStorageConfig(
        string AccountName,
        string AccountKey,
        string ContainerName,
        string? BlobEndpoint,
        string? PathPrefix);
}
