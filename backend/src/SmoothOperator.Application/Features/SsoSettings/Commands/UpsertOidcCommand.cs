using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

using SmoothOperator.Application.Interfaces.Sso;

namespace SmoothOperator.Application.Features.SsoSettings.Commands
{
    public sealed record UpsertOidcCommand(UpsertOidcRequest Request) : IRequest<SsoProviderDto>;

    public sealed class UpsertOidcCommandHandler : IRequestHandler<UpsertOidcCommand, SsoProviderDto>
    {
        private readonly ISsoProviderService _providers;
        private readonly IAuditService _audit;

        public UpsertOidcCommandHandler(ISsoProviderService providers, IAuditService audit)
        {
            _providers = providers;
            _audit = audit;
        }

        public async Task<SsoProviderDto> Handle(UpsertOidcCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;
            var existing = await _providers.GetDecryptedOidcAsync();

            var clientSecret = string.IsNullOrWhiteSpace(req.ClientSecret)
                ? existing?.ClientSecret ?? string.Empty
                : req.ClientSecret;

            if (string.IsNullOrEmpty(clientSecret))
                throw new BadRequestException("ClientSecret is required when configuring OIDC for the first time.");

            var cfg = new OidcConfig
            {
                Authority = req.Authority.Trim()
                    .TrimEnd('/')
                    .Replace("/.well-known/openid-configuration", string.Empty, StringComparison.OrdinalIgnoreCase),
                ClientId = req.ClientId.Trim(),
                ClientSecret = clientSecret,
                Scopes = string.IsNullOrWhiteSpace(req.Scopes) ? "openid profile email" : req.Scopes.Trim(),
                SubjectClaim = req.SubjectClaim.Trim(),
                EmailClaim = req.EmailClaim.Trim(),
                NameClaim = req.NameClaim.Trim(),
            };

            var saved = await _providers.UpsertOidcAsync(req.Name.Trim(), cfg);
            await _audit.WriteAsync("sso.config_updated", "sso", saved.Id.ToString(),
                new { type = "Oidc", saved.Name, authority = cfg.Authority });

            return await new Queries.GetSsoSettingsQueryHandler(_providers)
                .Handle(new Queries.GetSsoSettingsQuery(), cancellationToken);
        }
    }
}
