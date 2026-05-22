using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Infrastructure.Tests.Services
{
    public class WebhookEndpointCacheTests
    {
        private static (IServiceScopeFactory factory, ServiceProvider provider) BuildScopeFactory()
        {
            var services = new ServiceCollection();
            var dbName = "endpoint-cache-" + Guid.NewGuid().ToString("N");
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            var provider = services.BuildServiceProvider();
            return (provider.GetRequiredService<IServiceScopeFactory>(), provider);
        }

        private static WebhookEndpoint Endpoint(bool enabled = true, string eventTypes = "*")
            => new()
            {
                Id = Guid.NewGuid(),
                Name = "ep",
                Url = "https://example.com/hook",
                EncryptedSecret = "enc",
                EventTypes = eventTypes,
                Enabled = enabled,
                CreatedAt = DateTime.UtcNow,
            };

        [Fact]
        public async Task GetEnabledAsync_OnFirstCall_LoadsEnabledEndpoints()
        {
            var (factory, provider) = BuildScopeFactory();
            await using var _ = provider;
            var cache = new WebhookEndpointCache(factory, TimeSpan.FromMinutes(5));

            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.AddRange(
                    Endpoint(enabled: true, eventTypes: "user.*"),
                    Endpoint(enabled: false));
                await db.SaveChangesAsync();
            }

            var snap = await cache.GetEnabledAsync();

            snap.Should().HaveCount(1);
            snap[0].EventTypes.Should().Be("user.*");
        }

        [Fact]
        public async Task GetEnabledAsync_WithinTtl_ServesCachedSnapshot()
        {
            var (factory, provider) = BuildScopeFactory();
            await using var _ = provider;
            var cache = new WebhookEndpointCache(factory, TimeSpan.FromMinutes(5));

            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.Add(Endpoint());
                await db.SaveChangesAsync();
            }

            var first = await cache.GetEnabledAsync();
            first.Should().HaveCount(1);

            // Add a second row directly to the DB; the cache should still return one.
            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.Add(Endpoint());
                await db.SaveChangesAsync();
            }

            var second = await cache.GetEnabledAsync();
            second.Should().HaveCount(1);
        }

        [Fact]
        public async Task Invalidate_ForcesReloadOnNextCall()
        {
            var (factory, provider) = BuildScopeFactory();
            await using var _ = provider;
            var cache = new WebhookEndpointCache(factory, TimeSpan.FromMinutes(5));

            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.Add(Endpoint());
                await db.SaveChangesAsync();
            }
            (await cache.GetEnabledAsync()).Should().HaveCount(1);

            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.Add(Endpoint());
                await db.SaveChangesAsync();
            }
            cache.Invalidate();

            (await cache.GetEnabledAsync()).Should().HaveCount(2);
        }

        [Fact]
        public async Task GetEnabledAsync_AfterTtlElapsed_Reloads()
        {
            var (factory, provider) = BuildScopeFactory();
            await using var _ = provider;
            var cache = new WebhookEndpointCache(factory, TimeSpan.FromMilliseconds(50));

            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.Add(Endpoint());
                await db.SaveChangesAsync();
            }
            (await cache.GetEnabledAsync()).Should().HaveCount(1);

            using (var scope = factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.WebhookEndpoints.Add(Endpoint());
                await db.SaveChangesAsync();
            }

            await Task.Delay(120);
            (await cache.GetEnabledAsync()).Should().HaveCount(2);
        }
    }
}
