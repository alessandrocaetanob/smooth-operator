using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Auth.Queries
{
    public sealed record GetSetupStatusQuery : IRequest<SetupStatusResult>;

    public sealed record ProvidersStatus(bool Local, bool Sso, string? SsoType, string? SsoName);

    public sealed record SetupStatusResult(bool RequiresSetup, ProvidersStatus Providers);

    public sealed class GetSetupStatusQueryHandler : IRequestHandler<GetSetupStatusQuery, SetupStatusResult>
    {
        private readonly IAppDbContext _context;

        public GetSetupStatusQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<SetupStatusResult> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken)
        {
            var hasUsers = await _context.Users.AnyAsync(cancellationToken);
            var sso = await _context.SsoProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsEnabled, cancellationToken);

            var providers = new ProvidersStatus(
                Local: true,
                Sso: sso != null,
                SsoType: sso?.Type.ToString(),
                SsoName: sso?.Name);

            return new SetupStatusResult(!hasUsers, providers);
        }
    }
}
