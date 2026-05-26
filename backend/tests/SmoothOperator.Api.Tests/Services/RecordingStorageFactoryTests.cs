using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services.Recording;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public sealed class RecordingStorageFactoryTests
{
    private static (ServiceProvider provider, AppDbContext db, Mock<IEncryptionService> encryption) BuildScope()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString("N");
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => "decrypted:" + s);
        services.AddSingleton(encryption.Object);

        services.Configure<RecordingOptions>(o => o.DefaultStoragePath = "/tmp/so-test-default");
        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        return (provider, db, encryption);
    }

    private static RecordingStorageFactory NewFactory(ServiceProvider provider) => new(
        provider,
        provider.GetRequiredService<IEncryptionService>(),
        provider.GetRequiredService<IOptions<RecordingOptions>>());

    [Fact]
    public async Task CreateAsync_WhenNoSettings_ReturnsLocalWithDefaultPath()
    {
        var (provider, _, _) = BuildScope();
        using var factory = NewFactory(provider);

        var storage = await factory.CreateAsync(CancellationToken.None);

        Assert.Equal(RecordingStorageType.Local, storage.StorageType);
    }

    [Fact]
    public async Task CreateAsync_WhenLocalSettings_ReturnsLocal()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.Local,
            LocalPath = "/tmp/so-test-custom",
            RetentionDays = 90,
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        var storage = await factory.CreateAsync(CancellationToken.None);

        Assert.Equal(RecordingStorageType.Local, storage.StorageType);
    }

    [Fact]
    public async Task CreateAsync_WhenS3Settings_BuildsS3Adapter()
    {
        var (provider, db, encryption) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.S3,
            S3Bucket = "bucket",
            S3Region = "us-east-1",
            S3AccessKeyId = "AKIAEXAMPLE",
            EncryptedS3SecretAccessKey = "enc:supersecret",
            RetentionDays = 30,
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        var storage = await factory.CreateAsync(CancellationToken.None);

        Assert.Equal(RecordingStorageType.S3, storage.StorageType);
        encryption.Verify(e => e.Decrypt("enc:supersecret"), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenS3MissingBucket_Throws()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.S3,
            S3Bucket = null,
            S3AccessKeyId = "AKIAEXAMPLE",
            EncryptedS3SecretAccessKey = "enc:s",
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenS3MissingSecret_Throws()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.S3,
            S3Bucket = "bucket",
            S3AccessKeyId = "AKIAEXAMPLE",
            EncryptedS3SecretAccessKey = null,
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenAzureSettings_BuildsAzureAdapter()
    {
        var (provider, db, encryption) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.AzureBlob,
            AzureAccountName = "myacct",
            AzureContainerName = "recordings",
            EncryptedAzureAccountKey = "enc:" + Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
            RetentionDays = 30,
        });
        await db.SaveChangesAsync();

        encryption.Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("32-bytes-of-fake-key-aaaaaaaaaa")));

        using var factory = NewFactory(provider);
        var storage = await factory.CreateAsync(CancellationToken.None);

        Assert.Equal(RecordingStorageType.AzureBlob, storage.StorageType);
    }

    [Fact]
    public async Task CreateAsync_WhenAzureMissingAccount_Throws()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.AzureBlob,
            AzureAccountName = null,
            AzureContainerName = "recordings",
            EncryptedAzureAccountKey = "enc:k",
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenAzureMissingContainer_Throws()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.AzureBlob,
            AzureAccountName = "myacct",
            AzureContainerName = null,
            EncryptedAzureAccountKey = "enc:k",
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenAzureMissingKey_Throws()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.AzureBlob,
            AzureAccountName = "myacct",
            AzureContainerName = "recordings",
            EncryptedAzureAccountKey = null,
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateFor_WhenStorageTypeProvided_OverridesPersistedType()
    {
        // Persisted Local, but CreateFor(S3) re-resolves through the same builder with
        // the override applied to a copy of the settings row. S3Region MUST be set —
        // otherwise the AWS SDK falls back to IMDS lookup (169.254.169.254) and hangs
        // ~16s on a CI box with no instance metadata service.
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.Local,
            LocalPath = "/tmp/local",
            S3Bucket = "bucket",
            S3Region = "us-east-1",
            S3AccessKeyId = "AKIA",
            EncryptedS3SecretAccessKey = "enc:s",
            RetentionDays = 7,
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        var storage = factory.CreateFor(RecordingStorageType.S3);
        Assert.Equal(RecordingStorageType.S3, storage.StorageType);
    }

    [Fact]
    public void CreateFor_WhenNoSettings_Throws()
    {
        var (provider, _, _) = BuildScope();
        using var factory = NewFactory(provider);

        Assert.Throws<InvalidOperationException>(() => factory.CreateFor(RecordingStorageType.S3));
    }

    [Fact]
    public async Task CreateAsync_WhenLocalSettings_WithBlankPath_FallsBackToDefault()
    {
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.Local,
            LocalPath = "   ", // whitespace → fall back to RecordingOptions.DefaultStoragePath
            RetentionDays = 90,
        });
        await db.SaveChangesAsync();

        using var factory = NewFactory(provider);
        var storage = await factory.CreateAsync(CancellationToken.None);

        Assert.Equal(RecordingStorageType.Local, storage.StorageType);
    }

    [Fact]
    public async Task Dispose_DisposesTrackedAdapters()
    {
        // S3 adapter implements IDisposable; the factory must track + dispose to avoid socket leaks.
        var (provider, db, _) = BuildScope();
        db.RecordingStorageSettings.Add(new RecordingStorageSettings
        {
            Id = Guid.NewGuid(),
            StorageType = RecordingStorageType.S3,
            S3Bucket = "bucket",
            S3Region = "us-east-1",
            S3AccessKeyId = "AKIAEXAMPLE",
            EncryptedS3SecretAccessKey = "enc:supersecret",
        });
        await db.SaveChangesAsync();

        var factory = NewFactory(provider);
        var s3 = await factory.CreateAsync(CancellationToken.None);
        Assert.IsType<IDisposable>(s3, exactMatch: false);

        // Dispose factory — the tracked adapter is disposed too. Calling Dispose again
        // must be safe (S3RecordingStorageService guards on _disposed).
        factory.Dispose();
        var second = Record.Exception(() => (s3 as IDisposable)!.Dispose());
        Assert.Null(second);
    }
}
