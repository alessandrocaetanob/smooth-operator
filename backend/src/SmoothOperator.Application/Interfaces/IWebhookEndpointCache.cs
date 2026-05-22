using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// A short-TTL snapshot of every enabled webhook endpoint's
    /// <c>(Id, EventTypes)</c> tuple. The audit-write hot path consults this
    /// cache instead of querying the database on every event. Command handlers
    /// must call <see cref="Invalidate"/> after any mutation that affects the
    /// snapshot (create / update / delete).
    /// </summary>
    public interface IWebhookEndpointCache
    {
        Task<IReadOnlyList<EnabledWebhookEndpoint>> GetEnabledAsync(CancellationToken cancellationToken = default);

        /// <summary>Discards the current snapshot so the next read reloads from the database.</summary>
        void Invalidate();
    }

    /// <summary>Minimal projection used by the enqueuer hot path.</summary>
    public sealed record EnabledWebhookEndpoint(Guid Id, string EventTypes);
}
