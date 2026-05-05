using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.SecretProviders.Commands
{
    public sealed record DeleteSecretProviderCommand(Guid Id) : IRequest<bool>;

    public sealed class DeleteSecretProviderCommandHandler : IRequestHandler<DeleteSecretProviderCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteSecretProviderCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteSecretProviderCommand request, CancellationToken cancellationToken)
        {
            var provider = await _context.SecretProviders
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Secret provider not found.");

            // Prevent delete if any credentials are referencing this provider
            var inUse = await _context.Credentials
                .AnyAsync(c => c.SecretProviderId == request.Id, cancellationToken);

            if (inUse)
                throw new BadRequestException("Cannot delete a secret provider that is still referenced by credentials.");

            _context.SecretProviders.Remove(provider);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("provider.deleted", "SecretProvider", provider.Id.ToString(),
                new { provider.Name });

            return true;
        }
    }
}
