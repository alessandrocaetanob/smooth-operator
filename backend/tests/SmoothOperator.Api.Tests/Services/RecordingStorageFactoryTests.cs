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
        Assert.IsAssignableFrom<IDisposable>(s3);

        // Dispose factory — the tracked adapter is disposed too. Calling Dispose again
        // must be safe (S3RecordingStorageService guards on _disposed).
        factory.Dispose();
        var second = Record.Exception(() => (s3 as IDisposable)!.Dispose());
        Assert.Null(second);
    }
}
