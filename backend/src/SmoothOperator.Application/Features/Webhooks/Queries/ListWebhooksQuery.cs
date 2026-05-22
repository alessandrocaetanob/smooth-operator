using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Webhooks.Queries
{
    public sealed record ListWebhooksQuery : IRequest<IReadOnlyList<WebhookSummaryDto>>;

    public sealed class ListWebhooksQueryHandler
        : IRequestHandler<ListWebhooksQuery, IReadOnlyList<WebhookSummaryDto>>
    {
        private readonly IAppDbContext _context;

        public ListWebhooksQueryHandler(IAppDbContext context) => _context = context;

        public async Task<IReadOnlyList<WebhookSummaryDto>> Handle(
            ListWebhooksQuery request, CancellationToken cancellationToken)
        {
            return await _context.WebhookEndpoints
                .AsNoTracking()
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new WebhookSummaryDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Url = w.Url,
                    EventTypes = w.EventTypes,
                    Enabled = w.Enabled,
                    CreatedAt = w.CreatedAt,
                    UpdatedAt = w.UpdatedAt,
                    LastDeliveryAt = w.LastDeliveryAt,
                    LastDeliveryStatus = w.LastDeliveryStatus,
                    LastResponseCode = w.LastResponseCode,
                    ConsecutiveFailures = w.ConsecutiveFailures,
                })
                .ToListAsync(cancellationToken);
        }
    }
}
