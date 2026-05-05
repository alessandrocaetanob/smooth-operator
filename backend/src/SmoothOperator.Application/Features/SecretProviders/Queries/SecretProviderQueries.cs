using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Features.SecretProviders.Commands;

namespace SmoothOperator.Application.Features.SecretProviders.Queries
{
    public sealed record ListSecretProvidersQuery : IRequest<IReadOnlyList<SecretProviderDto>>;

    public sealed class ListSecretProvidersQueryHandler
        : IRequestHandler<ListSecretProvidersQuery, IReadOnlyList<SecretProviderDto>>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryption;

        public ListSecretProvidersQueryHandler(IAppDbContext context, IEncryptionService encryption)
        {
            _context = context;
            _encryption = encryption;
        }

        public async Task<IReadOnlyList<SecretProviderDto>> Handle(ListSecretProvidersQuery request, CancellationToken cancellationToken)
        {
            var providers = await _context.SecretProviders
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);

            return providers.ConvertAll(p => CreateSecretProviderCommandHandler.ToDto(p, _encryption));
        }
    }

    public sealed record GetSecretProviderQuery(Guid Id) : IRequest<SecretProviderDto>;

    public sealed class GetSecretProviderQueryHandler : IRequestHandler<GetSecretProviderQuery, SecretProviderDto>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryption;

        public GetSecretProviderQueryHandler(IAppDbContext context, IEncryptionService encryption)
        {
            _context = context;
            _encryption = encryption;
        }

        public async Task<SecretProviderDto> Handle(GetSecretProviderQuery request, CancellationToken cancellationToken)
        {
            var provider = await _context.SecretProviders
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new Exceptions.NotFoundException("Secret provider not found.");

            return CreateSecretProviderCommandHandler.ToDto(provider, _encryption);
        }
    }

    public sealed record ListSecretNamesQuery(Guid ProviderId) : IRequest<IReadOnlyList<string>>;

    public sealed class ListSecretNamesQueryHandler : IRequestHandler<ListSecretNamesQuery, IReadOnlyList<string>>
    {
        private readonly IAppDbContext _context;
        private readonly ISecretProviderFactory _factory;

        public ListSecretNamesQueryHandler(IAppDbContext context, ISecretProviderFactory factory)
        {
            _context = context;
            _factory = factory;
        }

        public async Task<IReadOnlyList<string>> Handle(ListSecretNamesQuery request, CancellationToken cancellationToken)
        {
            var provider = await _context.SecretProviders
                .FirstOrDefaultAsync(p => p.Id == request.ProviderId, cancellationToken)
                ?? throw new Exceptions.NotFoundException("Secret provider not found.");

            var secretProvider = _factory.Create(provider);
            return await secretProvider.ListSecretNamesAsync(cancellationToken);
        }
    }
}
