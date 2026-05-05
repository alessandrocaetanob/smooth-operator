using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Credentials.Commands
{
    public sealed record UpdateCredentialCommand(Guid Id, CreateCredentialDto Dto, ClaimsPrincipal User)
        : IRequest<bool>;

    public sealed class UpdateCredentialCommandHandler : IRequestHandler<UpdateCredentialCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditService _audit;

        public UpdateCredentialCommandHandler(IAppDbContext context, IEncryptionService encryptionService, IAuditService audit)
        {
            _context = context;
            _encryptionService = encryptionService;
            _audit = audit;
        }

        public async Task<bool> Handle(UpdateCredentialCommand request, CancellationToken cancellationToken)
        {
            var credential = await _context.Credentials
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (credential == null)
                throw new NotFoundException("Credential not found.");

            credential.Name = request.Dto.Name;
            credential.Username = request.Dto.Username;
            credential.CredentialType = request.Dto.CredentialType;
            credential.PublicKey = request.Dto.PublicKey;

            var secretRotated = false;
            if (!string.IsNullOrEmpty(request.Dto.Secret))
            {
                credential.EncryptedSecret = _encryptionService.Encrypt(request.Dto.Secret);
                secretRotated = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("credential.updated", "Credential", request.Id.ToString(),
                new { credential.Name, credential.Username, credential.CredentialType, secretRotated });

            return true;
        }
    }
}
