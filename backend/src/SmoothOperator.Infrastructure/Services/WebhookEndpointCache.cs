using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Singleton snapshot of every enabled webhook endpoint's
    /// <c>(Id, EventTypes)</c> tuple, refreshed at most every
    /// <see cref="DefaultTtl"/> or on demand via <see cref="Invalidate"/>.
    /// Audit writes are hot — without this cache, every audit log inserted
    /// would trigger a database query in the request scope.
    /// </summary>
    public class WebhookEndpointCache : IWebhookEndpointCache
    {
        internal static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _ttl;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private volatile IReadOnlyList<EnabledWebhookEndpoint>? _snapshot;
        private DateTime _expiresUtc = DateTime.MinValue;

        public WebhookEndpointCache(IServiceScopeFactory scopeFactory) : this(scopeFactory, DefaultTtl)
        {
        }

        internal WebhookEndpointCache(IServiceScopeFactory scopeFactory, TimeSpan ttl)
        {
            _scopeFactory = scopeFactory;
            _ttl = ttl;
        }

        public async Task<IReadOnlyList<EnabledWebhookEndpoint>> GetEnabledAsync(CancellationToken cancellationToken = default)
        {
            var local = _snapshot;
            if (local is not null && DateTime.UtcNow < _expiresUtc)
                return local;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_snapshot is not null && DateTime.UtcNow < _expiresUtc)
                    return _snapshot;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var rows = await db.WebhookEndpoints
                    .AsNoTracking()
                    .Where(w => w.Enabled)
                    .Select(w => new EnabledWebhookEndpoint(w.Id, w.EventTypes))
                    .ToListAsync(cancellationToken);

                _snapshot = rows;
                _expiresUtc = DateTime.UtcNow + _ttl;
                return rows;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Invalidate()
        {
            _snapshot = null;
            _expiresUtc = DateTime.MinValue;
        }
    }
}
