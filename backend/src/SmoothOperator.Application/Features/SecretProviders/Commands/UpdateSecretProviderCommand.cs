using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.SecretProviders.Commands
{
    public sealed record UpdateSecretProviderCommand(Guid Id, UpdateSecretProviderDto Dto) : IRequest<SecretProviderDto>;

    public sealed class UpdateSecretProviderCommandHandler : IRequestHandler<UpdateSecretProviderCommand, SecretProviderDto>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;

        public UpdateSecretProviderCommandHandler(IAppDbContext context, IEncryptionService encryption, IAuditService audit)
        {
            _context = context;
            _encryption = encryption;
            _audit = audit;
        }

        public async Task<SecretProviderDto> Handle(UpdateSecretProviderCommand request, CancellationToken cancellationToken)
        {
            var provider = await _context.SecretProviders
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Secret provider not found.");

            var dto = request.Dto;
            provider.Name = dto.Name;
            provider.IsEnabled = dto.IsEnabled;
            provider.UpdatedAt = DateTime.UtcNow;

            // Rebuild config if any KV fields are provided
            if (!string.IsNullOrWhiteSpace(dto.TenantId) ||
                !string.IsNullOrWhiteSpace(dto.ClientId) ||
                !string.IsNullOrWhiteSpace(dto.VaultUri) ||
                !string.IsNullOrWhiteSpace(dto.ClientSecret))
            {
                // Decrypt existing config to merge with partial update
                var existingJson = _encryption.Decrypt(provider.EncryptedConfigJson);
                var existing = JsonSerializer.Deserialize<AzureKeyVaultConfig>(existingJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new AzureKeyVaultConfig();

                var updated = new AzureKeyVaultConfig
                {
                    TenantId = dto.TenantId ?? existing.TenantId,
                    ClientId = dto.ClientId ?? existing.ClientId,
                    ClientSecret = string.IsNullOrWhiteSpace(dto.ClientSecret) ? existing.ClientSecret : dto.ClientSecret,
                    VaultUri = dto.VaultUri ?? existing.VaultUri
                };

                provider.EncryptedConfigJson = _encryption.Encrypt(JsonSerializer.Serialize(updated));
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("provider.updated", "SecretProvider", provider.Id.ToString(),
                new { provider.Name, provider.IsEnabled });

            return CreateSecretProviderCommandHandler.ToDto(provider, _encryption);
        }
    }
}
