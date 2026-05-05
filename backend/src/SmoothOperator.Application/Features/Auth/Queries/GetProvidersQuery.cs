using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Auth.Queries
{
    public sealed record GetProvidersQuery : IRequest<ProvidersResult>;

    public sealed record ProvidersResult(bool Local, bool Sso, string? SsoType, string? SsoName);

    public sealed class GetProvidersQueryHandler : IRequestHandler<GetProvidersQuery, ProvidersResult>
    {
        private readonly IAppDbContext _context;

        public GetProvidersQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ProvidersResult> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
        {
            var sso = await _context.SsoProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsEnabled, cancellationToken);

            return new ProvidersResult(
                Local: true,
                Sso: sso != null,
                SsoType: sso?.Type.ToString(),
                SsoName: sso?.Name);
        }
    }
}
