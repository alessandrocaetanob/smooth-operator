using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Features.RecordingStorageSettings.Queries;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.RecordingStorageSettings.Commands
{
    public sealed record SaveRecordingStorageSettingsCommand(UpdateRecordingStorageSettingsRequest Request)
        : IRequest<RecordingStorageSettingsDto>;

    public sealed class SaveRecordingStorageSettingsCommandHandler
        : IRequestHandler<SaveRecordingStorageSettingsCommand, RecordingStorageSettingsDto>
    {
        private static readonly Guid SingletonId = new("22222222-2222-2222-2222-222222222222");

        private readonly IAppDbContext _db;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;
        private readonly RecordingOptions _options;

        public SaveRecordingStorageSettingsCommandHandler(
            IAppDbContext db,
            IEncryptionService encryption,
            IAuditService audit,
            IOptions<RecordingOptions> options)
        {
            _db = db;
            _encryption = encryption;
            _audit = audit;
            _options = options.Value;
        }

        public async Task<RecordingStorageSettingsDto> Handle(
            SaveRecordingStorageSettingsCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;
            var s = await _db.RecordingStorageSettings.FirstOrDefaultAsync(cancellationToken);
            var creating = s == null;
            if (s == null)
            {
                s = new SmoothOperator.Domain.Models.RecordingStorageSettings { Id = SingletonId };
                _db.RecordingStorageSettings.Add(s);
            }

            s.StorageType = request.StorageType;
            s.LocalPath = string.IsNullOrWhiteSpace(request.LocalPath)
                ? _options.DefaultStoragePath
                : request.LocalPath.Trim();

            s.S3Bucket = NullIfBlank(request.S3Bucket);
            s.S3Region = NullIfBlank(request.S3Region);
            s.S3Endpoint = NullIfBlank(request.S3Endpoint);
            s.S3AccessKeyId = NullIfBlank(request.S3AccessKeyId);
            s.S3PathPrefix = NullIfBlank(request.S3PathPrefix);
            s.S3ForcePathStyle = request.S3ForcePathStyle;

            s.AzureAccountName = NullIfBlank(request.AzureAccountName);
            s.AzureContainerName = NullIfBlank(request.AzureContainerName);
            s.AzureBlobEndpoint = NullIfBlank(request.AzureBlobEndpoint);
            s.AzurePathPrefix = NullIfBlank(request.AzurePathPrefix);

            s.RetentionDays = request.RetentionDays;
            s.UpdatedAt = DateTime.UtcNow;

            // Secret handling: null => keep existing; "" => clear; non-empty => replace.
            if (request.S3SecretAccessKey != null)
            {
                s.EncryptedS3SecretAccessKey = request.S3SecretAccessKey.Length == 0
                    ? null
                    : _encryption.Encrypt(request.S3SecretAccessKey);
            }
            if (request.AzureAccountKey != null)
            {
                s.EncryptedAzureAccountKey = request.AzureAccountKey.Length == 0
                    ? null
                    : _encryption.Encrypt(request.AzureAccountKey);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                creating ? "recording.storage_settings.created" : "recording.storage_settings.updated",
                "RecordingStorageSettings", s.Id.ToString(),
                new { storageType = s.StorageType.ToString(), s.RetentionDays });

            return GetRecordingStorageSettingsQueryHandler.ToDto(s, _options.DefaultStoragePath);
        }

        private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
