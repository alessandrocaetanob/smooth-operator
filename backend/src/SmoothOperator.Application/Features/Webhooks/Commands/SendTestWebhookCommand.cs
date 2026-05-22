using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Webhooks.Commands
{
    public sealed record SendTestWebhookCommand(Guid Id) : IRequest;

    public sealed class SendTestWebhookCommandHandler : IRequestHandler<SendTestWebhookCommand>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public SendTestWebhookCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task Handle(SendTestWebhookCommand request, CancellationToken cancellationToken)
        {
            var endpoint = await _context.WebhookEndpoints
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Webhook endpoint not found.");

            // Queue a synthetic webhook.ping for the background worker to deliver.
            var deliveryId = Guid.NewGuid();
            _context.WebhookDeliveries.Add(new WebhookDelivery
            {
                Id = deliveryId,
                WebhookEndpointId = endpoint.Id,
                EventType = "webhook.ping",
                Payload = WebhookPayloadFactory.BuildPingPayload(deliveryId, endpoint),
                Status = WebhookDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                NextAttemptAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("webhook.test_sent", "WebhookEndpoint", endpoint.Id.ToString(),
                new { name = endpoint.Name });
        }
    }
}
