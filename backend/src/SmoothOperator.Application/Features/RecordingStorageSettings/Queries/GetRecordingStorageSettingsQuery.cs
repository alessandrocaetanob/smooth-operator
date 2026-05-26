using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Application.Features.RecordingStorageSettings.Queries
{
    public sealed record GetRecordingStorageSettingsQuery : IRequest<RecordingStorageSettingsDto>;

    public sealed class GetRecordingStorageSettingsQueryHandler
        : IRequestHandler<GetRecordingStorageSettingsQuery, RecordingStorageSettingsDto>
    {
        private readonly IAppDbContext _db;
        private readonly RecordingOptions _options;

        public GetRecordingStorageSettingsQueryHandler(IAppDbContext db, IOptions<RecordingOptions> options)
        {
            _db = db;
            _options = options.Value;
        }

        public async Task<RecordingStorageSettingsDto> Handle(
            GetRecordingStorageSettingsQuery request, CancellationToken cancellationToken)
        {
            var s = await _db.RecordingStorageSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            return ToDto(s, _options.DefaultStoragePath);
        }

        internal static RecordingStorageSettingsDto ToDto(
            SmoothOperator.Domain.Models.RecordingStorageSettings? s,
            string defaultLocalPath)
        {
            if (s == null)
            {
                // Surface the env-driven default so dev environments don't get the Docker path
                // (/var/recordings) they can't write to. Docker compose sets the env var to that path explicitly.
                return new RecordingStorageSettingsDto
                {
                    Configured = false,
                    LocalPath = defaultLocalPath,
                };
            }
            return new RecordingStorageSettingsDto
            {
                Configured = true,
                StorageType = s.StorageType,
                LocalPath = string.IsNullOrWhiteSpace(s.LocalPath) ? defaultLocalPath : s.LocalPath,
                S3Bucket = s.S3Bucket,
                S3Region = s.S3Region,
                S3Endpoint = s.S3Endpoint,
                S3AccessKeyId = s.S3AccessKeyId,
                HasS3SecretAccessKey = !string.IsNullOrEmpty(s.EncryptedS3SecretAccessKey),
                S3PathPrefix = s.S3PathPrefix,
                S3ForcePathStyle = s.S3ForcePathStyle,
                AzureAccountName = s.AzureAccountName,
                AzureContainerName = s.AzureContainerName,
                HasAzureAccountKey = !string.IsNullOrEmpty(s.EncryptedAzureAccountKey),
                AzureBlobEndpoint = s.AzureBlobEndpoint,
                AzurePathPrefix = s.AzurePathPrefix,
                RetentionDays = s.RetentionDays,
                UpdatedAt = s.UpdatedAt,
            };
        }
    }
}
