using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Webhooks.Commands
{
    public sealed record DeleteWebhookCommand(Guid Id) : IRequest;

    public sealed class DeleteWebhookCommandHandler : IRequestHandler<DeleteWebhookCommand>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteWebhookCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
        {
            var endpoint = await _context.WebhookEndpoints
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Webhook endpoint not found.");

            var name = endpoint.Name;

            // Queued WebhookDelivery rows cascade-delete with the endpoint.
            _context.WebhookEndpoints.Remove(endpoint);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("webhook.deleted", "WebhookEndpoint", request.Id.ToString(),
                new { name });
        }
    }
}
