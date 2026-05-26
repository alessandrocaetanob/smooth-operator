using System;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Domain.Models
{
    /// <summary>
    /// Singleton row holding the recording storage backend configuration.
    /// Mirrors the pattern of <see cref="SmtpSettings"/> — fixed Id, encrypted secrets.
    /// </summary>
    public class RecordingStorageSettings
    {
        public Guid Id { get; set; }

        public RecordingStorageType StorageType { get; set; } = RecordingStorageType.Local;

        /// <summary>
        /// Final destination for Local storage. Defaults to <c>/var/recordings</c>;
        /// overridable per deployment via the <c>Recording__DefaultStoragePath</c> env var.
        /// </summary>
        public string LocalPath { get; set; } = "/var/recordings";

        // S3 / S3-compatible (MinIO, R2, Backblaze, Wasabi)
        public string? S3Bucket { get; set; }
        public string? S3Region { get; set; }
        public string? S3Endpoint { get; set; }
        public string? S3AccessKeyId { get; set; }

        /// <summary>AES-256 encrypted S3 secret access key.</summary>
        public string? EncryptedS3SecretAccessKey { get; set; }

        public string? S3PathPrefix { get; set; }
        public bool S3ForcePathStyle { get; set; }

        // Azure Blob
        public string? AzureAccountName { get; set; }
        public string? AzureContainerName { get; set; }

        /// <summary>AES-256 encrypted Azure storage account key.</summary>
        public string? EncryptedAzureAccountKey { get; set; }

        public string? AzureBlobEndpoint { get; set; }
        public string? AzurePathPrefix { get; set; }

        /// <summary>Days to keep recordings before the retention sweep deletes them. 0 = keep forever.</summary>
        public int RetentionDays { get; set; } = 90;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
