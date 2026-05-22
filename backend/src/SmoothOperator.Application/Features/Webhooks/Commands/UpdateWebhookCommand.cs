using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Webhooks.Commands
{
    public sealed record UpdateWebhookCommand(
        Guid Id, string Name, string Url, string EventTypes, bool Enabled) : IRequest;

    public sealed class UpdateWebhookCommandHandler : IRequestHandler<UpdateWebhookCommand>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;
        private readonly IWebhookEndpointCache _cache;

        public UpdateWebhookCommandHandler(IAppDbContext context, IAuditService audit,
            IWebhookEndpointCache cache)
        {
            _context = context;
            _audit = audit;
            _cache = cache;
        }

        public async Task Handle(UpdateWebhookCommand request, CancellationToken cancellationToken)
        {
            var endpoint = await _context.WebhookEndpoints
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Webhook endpoint not found.");

            var (name, url, eventTypes) =
                WebhookInputValidator.Normalize(request.Name, request.Url, request.EventTypes);

            endpoint.Name = name;
            endpoint.Url = url;
            endpoint.EventTypes = eventTypes;
            endpoint.Enabled = request.Enabled;
            endpoint.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _cache.Invalidate();

            await _audit.WriteAsync("webhook.updated", "WebhookEndpoint", endpoint.Id.ToString(),
                new { name, url, eventTypes, enabled = request.Enabled });
        }
    }
}
