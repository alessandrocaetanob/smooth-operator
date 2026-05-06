using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Credentials.Commands
{
    public sealed record CreateCredentialCommand(CreateCredentialDto Dto, ClaimsPrincipal User)
        : IRequest<CredentialDto>;

    public sealed class CreateCredentialCommandHandler : IRequestHandler<CreateCredentialCommand, CredentialDto>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly ISecretProviderFactory _secretProviderFactory;
        private readonly IAuditService _audit;

        public CreateCredentialCommandHandler(
            IAppDbContext context,
            IEncryptionService encryptionService,
            ISecretProviderFactory secretProviderFactory,
            IAuditService audit)
        {
            _context = context;
            _encryptionService = encryptionService;
            _secretProviderFactory = secretProviderFactory;
            _audit = audit;
        }

        public async Task<CredentialDto> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;
            var isExternal = string.Equals(dto.StorageMode, "External", StringComparison.OrdinalIgnoreCase);

            var credential = new Credential
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Username = dto.Username,
                CredentialType = dto.CredentialType,
                PublicKey = dto.PublicKey,
                StorageMode = isExternal ? SecretStorageMode.External : SecretStorageMode.Local
            };

            if (isExternal)
            {
                if (dto.SecretProviderId == null)
                    throw new BadRequestException("SecretProviderId is required when StorageMode is External.");

                var provider = await _context.SecretProviders
                    .FirstOrDefaultAsync(p => p.Id == dto.SecretProviderId, cancellationToken)
                    ?? throw new NotFoundException("Secret provider not found.");

                credential.SecretProviderId = provider.Id;

                if (dto.PushToVault)
                {
                    // Push flow: write secret to KV, store only the reference
                    if (string.IsNullOrWhiteSpace(dto.Secret))
                        throw new BadRequestException("Secret value is required for push-to-vault flow.");

                    var secretProvider = _secretProviderFactory.Create(provider);
                    var secretName = $"smoothoperator-{SanitizeName(dto.Name)}-{Guid.NewGuid():N}";
                    await secretProvider.SetSecretAsync(secretName, dto.Secret!, cancellationToken);

                    credential.ExternalSecretName = secretName;
                    credential.ExternalSecretVersion = null; // track latest
                    credential.EncryptedSecret = string.Empty;

                    await _audit.WriteAsync("credential.pushed", "Credential", credential.Id.ToString(),
                        new { credential.Name, SecretName = secretName, ProviderId = provider.Id });
                }
                else
                {
                    // Link flow: reference an existing KV secret
                    if (string.IsNullOrWhiteSpace(dto.ExternalSecretName))
                        throw new BadRequestException("ExternalSecretName is required when linking to an existing vault secret.");

                    credential.ExternalSecretName = dto.ExternalSecretName;
                    credential.ExternalSecretVersion = dto.ExternalSecretVersion;
                    credential.EncryptedSecret = string.Empty;

                    await _audit.WriteAsync("credential.linked", "Credential", credential.Id.ToString(),
                        new { credential.Name, dto.ExternalSecretName, ProviderId = provider.Id });
                }
            }
            else
            {
                // Local flow
                if (string.IsNullOrWhiteSpace(dto.Secret))
                    throw new BadRequestException("Secret is required for local credentials.");

                credential.EncryptedSecret = _encryptionService.Encrypt(dto.Secret!);
            }

            _context.Credentials.Add(credential);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("credential.created", "Credential", credential.Id.ToString(),
                new { credential.Name, credential.Username, credential.CredentialType, credential.StorageMode });

            return ToDto(credential);
        }

        internal static CredentialDto ToDto(Credential c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Username = c.Username,
            CredentialType = c.CredentialType,
            PublicKey = c.PublicKey,
            StorageMode = c.StorageMode.ToString(),
            SecretProviderId = c.SecretProviderId,
            SecretProviderName = c.SecretProvider?.Name,
            ExternalSecretName = c.ExternalSecretName,
            ExternalSecretVersion = c.ExternalSecretVersion
        };

        private static string SanitizeName(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9\-]", "-", 
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }
}
