using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.SecretProviders.Commands
{
    public sealed record CreateSecretProviderCommand(CreateSecretProviderDto Dto) : IRequest<SecretProviderDto>;

    public sealed class CreateSecretProviderCommandHandler : IRequestHandler<CreateSecretProviderCommand, SecretProviderDto>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;

        public CreateSecretProviderCommandHandler(IAppDbContext context, IEncryptionService encryption, IAuditService audit)
        {
            _context = context;
            _encryption = encryption;
            _audit = audit;
        }

        public async Task<SecretProviderDto> Handle(CreateSecretProviderCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;

            if (dto.Type == SecretProviderType.AzureKeyVault)
            {
                if (string.IsNullOrWhiteSpace(dto.TenantId) ||
                    string.IsNullOrWhiteSpace(dto.ClientId) ||
                    string.IsNullOrWhiteSpace(dto.ClientSecret) ||
                    string.IsNullOrWhiteSpace(dto.VaultUri))
                {
                    throw new BadRequestException("TenantId, ClientId, ClientSecret, and VaultUri are required for Azure Key Vault providers.");
                }
            }

            var config = new AzureKeyVaultConfig
            {
                TenantId = dto.TenantId,
                ClientId = dto.ClientId,
                ClientSecret = dto.ClientSecret,
                VaultUri = dto.VaultUri
            };

            var configJson = JsonSerializer.Serialize(config);
            var encryptedConfig = _encryption.Encrypt(configJson);

            var provider = new SecretProvider
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Type = dto.Type,
                IsEnabled = dto.IsEnabled,
                EncryptedConfigJson = encryptedConfig,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.SecretProviders.Add(provider);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("provider.created", "SecretProvider", provider.Id.ToString(),
                new { provider.Name, provider.Type });

            return ToDto(provider, _encryption);
        }

        internal static SecretProviderDto ToDto(SecretProvider p, IEncryptionService? encryption = null)
        {
            AzureKeyVaultConfig? config = null;
            if (encryption != null && !string.IsNullOrEmpty(p.EncryptedConfigJson))
            {
                try
                {
                    var json = encryption.Decrypt(p.EncryptedConfigJson);
                    config = JsonSerializer.Deserialize<AzureKeyVaultConfig>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { /* return DTO without decrypted config fields on failure */ }
            }

            return new SecretProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type.ToString(),
                IsEnabled = p.IsEnabled,
                IsConfigured = !string.IsNullOrEmpty(p.EncryptedConfigJson),
                VaultUri = config?.VaultUri,
                TenantId = config?.TenantId,
                ClientId = config?.ClientId,
                HasClientSecret = config != null && !string.IsNullOrEmpty(config.ClientSecret),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
        }
    }
}
