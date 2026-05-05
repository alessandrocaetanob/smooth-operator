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
    public sealed record UpdateCredentialCommand(Guid Id, CreateCredentialDto Dto, ClaimsPrincipal User)
        : IRequest<bool>;

    public sealed class UpdateCredentialCommandHandler : IRequestHandler<UpdateCredentialCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly ISecretProviderFactory _secretProviderFactory;
        private readonly IAuditService _audit;

        public UpdateCredentialCommandHandler(
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

        public async Task<bool> Handle(UpdateCredentialCommand request, CancellationToken cancellationToken)
        {
            var credential = await _context.Credentials
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Credential not found.");

            var dto = request.Dto;
            var isExternal = string.Equals(dto.StorageMode, "External", StringComparison.OrdinalIgnoreCase);

            credential.Name = dto.Name;
            credential.Username = dto.Username;
            credential.CredentialType = dto.CredentialType;
            credential.PublicKey = dto.PublicKey;
            credential.StorageMode = isExternal ? SecretStorageMode.External : SecretStorageMode.Local;

            var secretRotated = false;

            if (isExternal)
            {
                if (dto.SecretProviderId == null)
                    throw new BadRequestException("SecretProviderId is required when StorageMode is External.");

                var provider = await _context.SecretProviders
                    .FirstOrDefaultAsync(p => p.Id == dto.SecretProviderId, cancellationToken)
                    ?? throw new NotFoundException("Secret provider not found.");

                credential.SecretProviderId = provider.Id;
                credential.EncryptedSecret = string.Empty;

                if (dto.PushToVault && !string.IsNullOrWhiteSpace(dto.Secret))
                {
                    var secretProvider = _secretProviderFactory.Create(provider);
                    var secretName = credential.ExternalSecretName
                        ?? $"smoothoperator-{Sanitize(dto.Name)}-{Guid.NewGuid():N}";
                    await secretProvider.SetSecretAsync(secretName, dto.Secret!, cancellationToken);
                    credential.ExternalSecretName = secretName;
                    credential.ExternalSecretVersion = null;
                    secretRotated = true;

                    await _audit.WriteAsync("credential.pushed", "Credential", credential.Id.ToString(),
                        new { credential.Name, SecretName = secretName, ProviderId = provider.Id });
                }
                else if (!string.IsNullOrWhiteSpace(dto.ExternalSecretName))
                {
                    credential.ExternalSecretName = dto.ExternalSecretName;
                    credential.ExternalSecretVersion = dto.ExternalSecretVersion;

                    await _audit.WriteAsync("credential.linked", "Credential", credential.Id.ToString(),
                        new { credential.Name, dto.ExternalSecretName, ProviderId = provider.Id });
                }
            }
            else
            {
                // Local: clear external refs
                credential.SecretProviderId = null;
                credential.ExternalSecretName = null;
                credential.ExternalSecretVersion = null;

                if (!string.IsNullOrEmpty(dto.Secret))
                {
                    credential.EncryptedSecret = _encryptionService.Encrypt(dto.Secret!);
                    secretRotated = true;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("credential.updated", "Credential", request.Id.ToString(),
                new { credential.Name, credential.Username, credential.CredentialType, secretRotated, credential.StorageMode });

            return true;
        }

        private static string Sanitize(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9\-]", "-");
    }
}
