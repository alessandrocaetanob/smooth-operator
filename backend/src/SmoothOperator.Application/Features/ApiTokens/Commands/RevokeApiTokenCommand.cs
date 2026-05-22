using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.ApiTokens.Commands
{
    public sealed record RevokeApiTokenCommand(Guid UserId, Guid TokenId) : IRequest;

    public sealed class RevokeApiTokenCommandHandler : IRequestHandler<RevokeApiTokenCommand>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public RevokeApiTokenCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task Handle(RevokeApiTokenCommand request, CancellationToken cancellationToken)
        {
            var token = await _context.ApiTokens
                .FirstOrDefaultAsync(t => t.Id == request.TokenId && t.UserId == request.UserId, cancellationToken)
                ?? throw new NotFoundException("API token not found.");

            if (token.RevokedAt is not null)
                return;

            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.api_token_revoked", "ApiToken", token.Id.ToString(),
                new { name = token.Name });
        }
    }
}
