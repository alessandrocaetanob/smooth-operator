using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Webhooks.Commands
{
    public sealed record CreateWebhookCommand(string Name, string Url, string EventTypes)
        : IRequest<WebhookSecretResult>;

    public sealed class CreateWebhookCommandHandler
        : IRequestHandler<CreateWebhookCommand, WebhookSecretResult>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;

        public CreateWebhookCommandHandler(
            IAppDbContext context, IEncryptionService encryption, IAuditService audit)
        {
            _context = context;
            _encryption = encryption;
            _audit = audit;
        }

        public async Task<WebhookSecretResult> Handle(
            CreateWebhookCommand request, CancellationToken cancellationToken)
        {
            var (name, url, eventTypes) =
                WebhookInputValidator.Normalize(request.Name, request.Url, request.EventTypes);

            var secret = WebhookSecretGenerator.Generate();

            var endpoint = new WebhookEndpoint
            {
                Id = Guid.NewGuid(),
                Name = name,
                Url = url,
                EventTypes = eventTypes,
                EncryptedSecret = _encryption.Encrypt(secret),
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
            };

            _context.WebhookEndpoints.Add(endpoint);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("webhook.created", "WebhookEndpoint", endpoint.Id.ToString(),
                new { name, url, eventTypes });

            return new WebhookSecretResult
            {
                Id = endpoint.Id,
                Name = name,
                Secret = secret,
            };
        }
    }
}
