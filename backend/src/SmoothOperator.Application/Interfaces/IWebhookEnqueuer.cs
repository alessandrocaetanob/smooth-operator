using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// Fans an audit event out into <c>WebhookDelivery</c> rows, one per enabled
    /// endpoint subscribed to the event. Implementations add rows to the shared
    /// scoped <c>DbContext</c> but must NOT call <c>SaveChanges</c> — the audit
    /// write's own commit is the transactional-outbox boundary.
    /// </summary>
    public interface IWebhookEnqueuer
    {
        Task EnqueueAsync(AuditLog entry, CancellationToken cancellationToken = default);
    }
}
