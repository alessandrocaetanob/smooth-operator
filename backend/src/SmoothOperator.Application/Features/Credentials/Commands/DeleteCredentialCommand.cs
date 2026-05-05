using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Credentials.Commands
{
    public sealed record DeleteCredentialCommand(Guid Id, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class DeleteCredentialCommandHandler : IRequestHandler<DeleteCredentialCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteCredentialCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteCredentialCommand request, CancellationToken cancellationToken)
        {
            var credential = await _context.Credentials
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (credential == null)
                throw new NotFoundException("Credential not found.");

            _context.Credentials.Remove(credential);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("credential.deleted", "Credential", request.Id.ToString(),
                new { credential.Name });

            return true;
        }
    }
}
