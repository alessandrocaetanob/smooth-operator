using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.SecretProviders.Commands
{
    public sealed record TestSecretProviderConnectionCommand(Guid Id) : IRequest<bool>;

    public sealed class TestSecretProviderConnectionCommandHandler
        : IRequestHandler<TestSecretProviderConnectionCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly ISecretProviderFactory _factory;
        private readonly IAuditService _audit;

        public TestSecretProviderConnectionCommandHandler(
            IAppDbContext context,
            ISecretProviderFactory factory,
            IAuditService audit)
        {
            _context = context;
            _factory = factory;
            _audit = audit;
        }

        public async Task<bool> Handle(TestSecretProviderConnectionCommand request, CancellationToken cancellationToken)
        {
            var provider = await _context.SecretProviders
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Secret provider not found.");

            var secretProvider = _factory.Create(provider);
            var success = await secretProvider.TestConnectionAsync(cancellationToken);

            await _audit.WriteAsync("provider.test_connection", "SecretProvider", provider.Id.ToString(),
                new { provider.Name, success });

            return success;
        }
    }
}
