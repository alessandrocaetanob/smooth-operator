using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Mfa.Commands
{
    public sealed record DisableMfaCommand(Guid UserId, string Password) : IRequest;

    public sealed class DisableMfaCommandHandler : IRequestHandler<DisableMfaCommand>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DisableMfaCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task Handle(DisableMfaCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedException("Password is incorrect.");

            var credential = await _context.MfaCredentials
                .FirstOrDefaultAsync(m => m.UserId == request.UserId, cancellationToken)
                ?? throw new NotFoundException("MFA is not enabled.");

            _context.MfaCredentials.Remove(credential);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.mfa_disabled", "User", request.UserId.ToString(), new { });
        }
    }
}
