using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
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
        private readonly IAuditService _audit;

        public CreateCredentialCommandHandler(IAppDbContext context, IEncryptionService encryptionService, IAuditService audit)
        {
            _context = context;
            _encryptionService = encryptionService;
            _audit = audit;
        }

        public async Task<CredentialDto> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
        {
            var credential = new Credential
            {
                Id = Guid.NewGuid(),
                Name = request.Dto.Name,
                Username = request.Dto.Username,
                CredentialType = request.Dto.CredentialType,
                PublicKey = request.Dto.PublicKey,
                EncryptedSecret = _encryptionService.Encrypt(request.Dto.Secret)
            };

            _context.Credentials.Add(credential);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("credential.created", "Credential", credential.Id.ToString(),
                new { credential.Name, credential.Username, credential.CredentialType });

            return new CredentialDto
            {
                Id = credential.Id,
                Name = credential.Name,
                Username = credential.Username,
                CredentialType = credential.CredentialType,
                PublicKey = credential.PublicKey
            };
        }
    }
}
