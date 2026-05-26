using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Infrastructure.Services.Recording
{
    /// <summary>
    /// S3 / S3-compatible (MinIO, Backblaze B2, Cloudflare R2, Wasabi) storage backend.
    /// </summary>
    public sealed class S3RecordingStorageService : IRecordingStorageService, IDisposable
    {
        private readonly IAmazonS3 _client;
        private readonly string _bucket;
        private readonly string? _prefix;
        private bool _disposed;

        public S3RecordingStorageService(S3StorageConfig config)
            : this(BuildClient(config), config.Bucket, config.PathPrefix)
        {
        }

        internal S3RecordingStorageService(IAmazonS3 client, string bucket, string? pathPrefix)
        {
            _client = client;
            _bucket = bucket;
            _prefix = NormalisePrefix(pathPrefix);
        }

        private static AmazonS3Client BuildClient(S3StorageConfig config)
        {
            var s3Config = new AmazonS3Config();
            if (!string.IsNullOrWhiteSpace(config.Endpoint))
                s3Config.ServiceURL = config.Endpoint;
            else if (!string.IsNullOrWhiteSpace(config.Region))
                s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);

            s3Config.ForcePathStyle = config.ForcePathStyle;

            var creds = new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);
            return new AmazonS3Client(creds, s3Config);
        }

        public RecordingStorageType StorageType => RecordingStorageType.S3;

        public async Task<RecordingUploadResult> UploadAsync(string localFilePath, string storageKey, CancellationToken cancellationToken)
        {
            var key = ResolveKey(storageKey);
            // TransferUtility handles multi-part for files > 16 MB.
            using var transferUtility = new TransferUtility(_client);
            await transferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = _bucket,
                Key = key,
                FilePath = localFilePath,
                ContentType = "application/octet-stream",
            }, cancellationToken);

            var size = new FileInfo(localFilePath).Length;
            return new RecordingUploadResult(storageKey, size);
        }

        public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            var key = ResolveKey(storageKey);
            var resp = await _client.GetObjectAsync(new GetObjectRequest { BucketName = _bucket, Key = key }, cancellationToken);
            return resp.ResponseStream;
        }

        public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            var key = ResolveKey(storageKey);
            await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, cancellationToken);
        }

        public async Task<string?> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = _bucket }, cancellationToken);
                return null;
            }
            catch (AmazonS3Exception ex)
            {
                return $"S3: {ex.ErrorCode} — {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"S3: {ex.Message}";
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client.Dispose();
        }
    }

    public sealed record S3StorageConfig(
        string Bucket,
        string? Region,
        string? Endpoint,
        string AccessKeyId,
        string SecretAccessKey,
        string? PathPrefix,
        bool ForcePathStyle);
}
