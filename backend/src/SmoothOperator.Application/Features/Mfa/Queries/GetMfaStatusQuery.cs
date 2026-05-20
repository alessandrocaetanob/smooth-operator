using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Mfa.Queries
{
    public sealed record GetMfaStatusQuery(Guid UserId) : IRequest<MfaStatusResult>;

    public sealed record MfaStatusResult(bool IsEnabled, int RecoveryCodesRemaining);

    public sealed class GetMfaStatusQueryHandler : IRequestHandler<GetMfaStatusQuery, MfaStatusResult>
    {
        private readonly IAppDbContext _context;

        public GetMfaStatusQueryHandler(IAppDbContext context) => _context = context;

        public async Task<MfaStatusResult> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
        {
            var credential = await _context.MfaCredentials
                .Include(m => m.RecoveryCodes)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == request.UserId, cancellationToken);

            if (credential is null or { IsEnabled: false })
                return new MfaStatusResult(false, 0);

            var remaining = credential.RecoveryCodes.Count(r => !r.IsUsed);
            return new MfaStatusResult(true, remaining);
        }
    }
}
