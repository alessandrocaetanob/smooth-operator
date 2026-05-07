using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace SmoothOperator.Api.Extensions;

public static class CompressionExtensions
{
    public static IServiceCollection AddApplicationResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(opts =>
        {
            opts.EnableForHttps = true;
            opts.Providers.Add<BrotliCompressionProvider>();
            opts.Providers.Add<GzipCompressionProvider>();
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/json", "image/svg+xml", "application/javascript"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(opts =>
            opts.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(opts =>
            opts.Level = CompressionLevel.SmallestSize);
        return services;
    }

    public static IServiceCollection AddApplicationOutputCache(this IServiceCollection services)
    {
        services.AddOutputCache(opts =>
            opts.AddPolicy("ShortCache", policy => policy.Expire(TimeSpan.FromSeconds(30))));
        return services;
    }
}
