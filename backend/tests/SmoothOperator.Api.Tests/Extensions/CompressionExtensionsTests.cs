using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Extensions;
using Xunit;

namespace SmoothOperator.Api.Tests.Extensions;

public class CompressionExtensionsTests
{
    private static IConfiguration BuildConfig(bool useRedis) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:UseRedis"] = useRedis ? "true" : "false",
                ["ConnectionStrings:Redis"] = "localhost:6379",
            })
            .Build();

    [Fact]
    public void AddApplicationOutputCache_InMemory_RegistersOutputCacheStore()
    {
        var services = new ServiceCollection();

        services.AddApplicationOutputCache(BuildConfig(useRedis: false));

        Assert.Contains(services, d => d.ServiceType == typeof(IOutputCacheStore));
    }

    [Fact]
    public void AddApplicationOutputCache_WithRedisEnabled_RegistersAdditionalServices()
    {
        var inMemory = new ServiceCollection();
        inMemory.AddApplicationOutputCache(BuildConfig(useRedis: false));

        var withRedis = new ServiceCollection();
        withRedis.AddApplicationOutputCache(BuildConfig(useRedis: true));

        // The Cache:UseRedis branch registers the StackExchange.Redis-backed
        // output-cache store on top of the base AddOutputCache registrations.
        Assert.True(withRedis.Count > inMemory.Count);
    }
}
