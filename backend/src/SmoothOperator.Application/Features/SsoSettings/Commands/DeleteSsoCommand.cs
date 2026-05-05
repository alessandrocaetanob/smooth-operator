using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;

namespace SmoothOperator.Application.Features.SsoSettings.Commands
{
    public sealed record DeleteSsoCommand : IRequest<bool>;

    public sealed class DeleteSsoCommandHandler : IRequestHandler<DeleteSsoCommand, bool>
    {
        private readonly ISsoProviderService _providers;
        private readonly IAuditService _audit;

        public DeleteSsoCommandHandler(ISsoProviderService providers, IAuditService audit)
        {
            _providers = providers;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteSsoCommand request, CancellationToken cancellationToken)
        {
            var existing = await _providers.GetProviderAsync();
            var ok = await _providers.DeleteAsync();
            if (!ok)
                throw new NotFoundException("No SSO provider is configured.");

            await _audit.WriteAsync("sso.config_deleted", "sso", existing?.Id.ToString() ?? "",
                new { type = existing?.Type.ToString() });

            return true;
        }
    }
}
