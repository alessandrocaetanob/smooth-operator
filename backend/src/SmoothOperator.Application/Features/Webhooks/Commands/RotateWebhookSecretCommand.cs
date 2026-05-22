using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Webhooks.Commands
{
    public sealed record RotateWebhookSecretCommand(Guid Id) : IRequest<WebhookSecretResult>;

    public sealed class RotateWebhookSecretCommandHandler
        : IRequestHandler<RotateWebhookSecretCommand, WebhookSecretResult>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;

        public RotateWebhookSecretCommandHandler(
            IAppDbContext context, IEncryptionService encryption, IAuditService audit)
        {
            _context = context;
            _encryption = encryption;
            _audit = audit;
        }

        public async Task<WebhookSecretResult> Handle(
            RotateWebhookSecretCommand request, CancellationToken cancellationToken)
        {
            var endpoint = await _context.WebhookEndpoints
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Webhook endpoint not found.");

            var secret = WebhookSecretGenerator.Generate();
            endpoint.EncryptedSecret = _encryption.Encrypt(secret);
            endpoint.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("webhook.secret_rotated", "WebhookEndpoint", endpoint.Id.ToString(),
                new { name = endpoint.Name });

            return new WebhookSecretResult
            {
                Id = endpoint.Id,
                Name = endpoint.Name,
                Secret = secret,
            };
        }
    }
}
