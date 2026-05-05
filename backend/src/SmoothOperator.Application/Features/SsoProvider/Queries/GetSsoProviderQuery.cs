using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces.Sso;

namespace SmoothOperator.Application.Features.SsoProvider.Queries
{
    public sealed record GetSsoProviderQuery : IRequest<SsoStatusDto>;

    public sealed class GetSsoProviderQueryHandler : IRequestHandler<GetSsoProviderQuery, SsoStatusDto>
    {
        private readonly ISsoProviderService _providers;

        public GetSsoProviderQueryHandler(ISsoProviderService providers) => _providers = providers;

        public async Task<SsoStatusDto> Handle(GetSsoProviderQuery request, CancellationToken cancellationToken)
        {
            var p = await _providers.GetActiveProviderAsync();
            if (p == null)
                return new SsoStatusDto { Enabled = false };

            return new SsoStatusDto
            {
                Enabled = p.IsEnabled,
                Type = p.Type.ToString(),
                Name = p.Name,
            };
        }
    }
}
