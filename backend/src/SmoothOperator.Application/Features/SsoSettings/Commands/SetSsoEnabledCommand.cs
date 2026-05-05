using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;

namespace SmoothOperator.Application.Features.SsoSettings.Commands
{
    public sealed record SetSsoEnabledCommand(bool Enabled) : IRequest<SsoProviderDto?>;

    public sealed class SetSsoEnabledCommandHandler : IRequestHandler<SetSsoEnabledCommand, SsoProviderDto?>
    {
        private readonly ISsoProviderService _providers;
        private readonly IAuditService _audit;

        public SetSsoEnabledCommandHandler(ISsoProviderService providers, IAuditService audit)
        {
            _providers = providers;
            _audit = audit;
        }

        public async Task<SsoProviderDto?> Handle(SetSsoEnabledCommand request, CancellationToken cancellationToken)
        {
            var p = await _providers.SetEnabledAsync(request.Enabled);
            if (p == null)
                throw new NotFoundException("Configure an SSO provider before enabling it.");

            await _audit.WriteAsync(
                request.Enabled ? "sso.enabled" : "sso.disabled",
                "sso",
                p.Id.ToString(),
                new { type = p.Type.ToString() });

            return await new Queries.GetSsoSettingsQueryHandler(_providers)
                .Handle(new Queries.GetSsoSettingsQuery(), cancellationToken);
        }
    }
}
