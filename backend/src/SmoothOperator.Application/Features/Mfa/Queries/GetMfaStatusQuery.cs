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
            var status = await _context.MfaCredentials
                .AsNoTracking()
                .Where(m => m.UserId == request.UserId)
                .Select(m => new { m.IsEnabled, Remaining = m.RecoveryCodes.Count(r => !r.IsUsed) })
                .FirstOrDefaultAsync(cancellationToken);

            if (status is null || !status.IsEnabled)
                return new MfaStatusResult(false, 0);

            return new MfaStatusResult(true, status.Remaining);
        }
    }
}
