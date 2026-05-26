using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services.Recording
{
    /// <summary>
    /// Materialises the right <see cref="IRecordingStorageService"/> from the singleton
    /// <c>RecordingStorageSettings</c> row. Secrets are decrypted via <see cref="IEncryptionService"/>
    /// just before constructing the adapter — they never live on the entity in plain form.
    /// </summary>
    public sealed class RecordingStorageFactory : IRecordingStorageFactory, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IEncryptionService _encryption;
        private readonly RecordingOptions _options;
        private readonly List<IDisposable> _disposables = new();

        public RecordingStorageFactory(
            IServiceProvider services,
            IEncryptionService encryption,
            IOptions<RecordingOptions> options)
        {
            _services = services;
            _encryption = encryption;
            _options = options.Value;
        }

        public async Task<IRecordingStorageService> CreateAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var settings = await db.RecordingStorageSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

            // No settings yet → behave as Local with the env-var default. Lets the feature
            // work out-of-the-box on a fresh install before an admin configures cloud storage.
            if (settings == null)
                return Track(new LocalRecordingStorageService(_options.DefaultStoragePath));

            return BuildFromSettings(settings);
        }

        public IRecordingStorageService CreateFor(RecordingStorageType storageType)
        {
            // Used by the "test" endpoint — caller provides candidate settings via the
            // same SaveRecordingStorageSettingsCommand DTO, persisted before calling Test.
            // Resolution still goes through CreateAsync so the test reads what's saved.
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = db.RecordingStorageSettings.AsNoTracking().FirstOrDefault()
                ?? throw new InvalidOperationException("Recording storage settings have not been configured.");
            settings.StorageType = storageType;
            return BuildFromSettings(settings);
        }

        private IRecordingStorageService BuildFromSettings(Domain.Models.RecordingStorageSettings s)
        {
            IRecordingStorageService service = s.StorageType switch
            {
                RecordingStorageType.Local => new LocalRecordingStorageService(
                    string.IsNullOrWhiteSpace(s.LocalPath) ? _options.DefaultStoragePath : s.LocalPath),

                RecordingStorageType.S3 => new S3RecordingStorageService(new S3StorageConfig(
                    Bucket: s.S3Bucket ?? throw MissingField(nameof(s.S3Bucket)),
                    Region: s.S3Region,
                    Endpoint: s.S3Endpoint,
                    AccessKeyId: s.S3AccessKeyId ?? throw MissingField(nameof(s.S3AccessKeyId)),
                    SecretAccessKey: DecryptOrThrow(s.EncryptedS3SecretAccessKey, "S3 secret access key"),
                    PathPrefix: s.S3PathPrefix,
                    ForcePathStyle: s.S3ForcePathStyle)),

                RecordingStorageType.AzureBlob => new AzureBlobRecordingStorageService(new AzureBlobStorageConfig(
                    AccountName: s.AzureAccountName ?? throw MissingField(nameof(s.AzureAccountName)),
                    AccountKey: DecryptOrThrow(s.EncryptedAzureAccountKey, "Azure account key"),
                    ContainerName: s.AzureContainerName ?? throw MissingField(nameof(s.AzureContainerName)),
                    BlobEndpoint: s.AzureBlobEndpoint,
                    PathPrefix: s.AzurePathPrefix)),

                _ => throw new NotSupportedException($"Recording storage type '{s.StorageType}' is not supported."),
            };

            return Track(service);
        }

        // Track disposable adapters so they ride the request scope; the AWS S3 client
        // owns an HTTP connection pool and leaks sockets if never disposed.
        private IRecordingStorageService Track(IRecordingStorageService service)
        {
            if (service is IDisposable disposable)
                _disposables.Add(disposable);
            return service;
        }

        private string DecryptOrThrow(string? encrypted, string label)
        {
            if (string.IsNullOrEmpty(encrypted))
                throw new InvalidOperationException($"Recording storage is missing required secret: {label}.");
            return _encryption.Decrypt(encrypted);
        }

        private static InvalidOperationException MissingField(string field) =>
            new($"Recording storage is missing required field: {field}.");

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try { disposable.Dispose(); }
                catch { /* swallow — best-effort cleanup */ }
            }
            _disposables.Clear();
        }
    }
}
