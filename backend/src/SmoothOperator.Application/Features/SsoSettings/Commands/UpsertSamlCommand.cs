using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;


namespace SmoothOperator.Application.Features.SsoSettings.Commands
{
    public sealed record UpsertSamlCommand(UpsertSamlRequest Request) : IRequest<SsoProviderDto>;

    public sealed class UpsertSamlCommandHandler : IRequestHandler<UpsertSamlCommand, SsoProviderDto>
    {
        private readonly ISsoProviderService _providers;
        private readonly IAuditService _audit;

        public UpsertSamlCommandHandler(ISsoProviderService providers, IAuditService audit)
        {
            _providers = providers;
            _audit = audit;
        }

        public async Task<SsoProviderDto> Handle(UpsertSamlCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;
            var existing = await _providers.GetDecryptedSamlAsync();

            var spPrivateKey = string.IsNullOrWhiteSpace(req.SpPrivateKey)
                ? existing?.SpPrivateKey ?? string.Empty
                : req.SpPrivateKey;

            if (string.IsNullOrEmpty(spPrivateKey))
                throw new BadRequestException("SpPrivateKey is required when configuring SAML for the first time.");

            var cfg = new SamlConfig
            {
                SpEntityId = req.SpEntityId.Trim(),
                IdpEntityId = req.IdpEntityId.Trim(),
                IdpSsoUrl = req.IdpSsoUrl.Trim(),
                IdpCertificate = req.IdpCertificate.Trim(),
                SpCertificate = req.SpCertificate.Trim(),
                SpPrivateKey = spPrivateKey,
                NameIdFormat = req.NameIdFormat.Trim(),
                AttributeEmail = req.AttributeEmail.Trim(),
                AttributeName = req.AttributeName.Trim(),
                WantAssertionsSigned = req.WantAssertionsSigned,
                WantResponseSigned = req.WantResponseSigned,
            };

            var saved = await _providers.UpsertSamlAsync(req.Name.Trim(), cfg);
            await _audit.WriteAsync("sso.config_updated", "sso", saved.Id.ToString(),
                new { type = "Saml", saved.Name, idpEntityId = cfg.IdpEntityId });

            return await new Queries.GetSsoSettingsQueryHandler(_providers)
                .Handle(new Queries.GetSsoSettingsQuery(), cancellationToken);
        }
    }
}
