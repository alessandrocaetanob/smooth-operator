using System;
using System.ComponentModel.DataAnnotations;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.DTOs
{
    public class RecordingStorageSettingsDto
    {
        public bool Configured { get; set; }

        public RecordingStorageType StorageType { get; set; } = RecordingStorageType.Local;

        public string LocalPath { get; set; } = "/var/recordings";

        public string? S3Bucket { get; set; }
        public string? S3Region { get; set; }
        public string? S3Endpoint { get; set; }
        public string? S3AccessKeyId { get; set; }

        /// <summary>True when a stored S3 secret access key exists. The secret itself is never returned.</summary>
        public bool HasS3SecretAccessKey { get; set; }
        public string? S3PathPrefix { get; set; }
        public bool S3ForcePathStyle { get; set; }

        public string? AzureAccountName { get; set; }
        public string? AzureContainerName { get; set; }

        /// <summary>True when a stored Azure account key exists. The secret itself is never returned.</summary>
        public bool HasAzureAccountKey { get; set; }
        public string? AzureBlobEndpoint { get; set; }
        public string? AzurePathPrefix { get; set; }

        public int RetentionDays { get; set; } = 90;
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateRecordingStorageSettingsRequest
    {
        public RecordingStorageType StorageType { get; set; } = RecordingStorageType.Local;

        [StringLength(512)]
        public string LocalPath { get; set; } = "/var/recordings";

        [StringLength(255)]
        public string? S3Bucket { get; set; }

        [StringLength(64)]
        public string? S3Region { get; set; }

        [StringLength(512)]
        public string? S3Endpoint { get; set; }

        [StringLength(255)]
        public string? S3AccessKeyId { get; set; }

        /// <summary>When null on update the existing secret is kept; empty string clears it.</summary>
        [StringLength(512)]
        public string? S3SecretAccessKey { get; set; }

        [StringLength(512)]
        public string? S3PathPrefix { get; set; }

        public bool S3ForcePathStyle { get; set; }

        [StringLength(255)]
        public string? AzureAccountName { get; set; }

        [StringLength(255)]
        public string? AzureContainerName { get; set; }

        /// <summary>When null on update the existing key is kept; empty string clears it.</summary>
        [StringLength(1024)]
        public string? AzureAccountKey { get; set; }

        [StringLength(512)]
        public string? AzureBlobEndpoint { get; set; }

        [StringLength(512)]
        public string? AzurePathPrefix { get; set; }

        [Range(0, 3650)]
        public int RetentionDays { get; set; } = 90;
    }

    public class RecordingStorageTestResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
