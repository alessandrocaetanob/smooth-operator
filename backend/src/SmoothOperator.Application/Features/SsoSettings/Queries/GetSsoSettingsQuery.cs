using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.SsoSettings.Queries
{
    public sealed record GetSsoSettingsQuery : IRequest<SsoProviderDto>;

    public sealed class GetSsoSettingsQueryHandler : IRequestHandler<GetSsoSettingsQuery, SsoProviderDto>
    {
        private readonly ISsoProviderService _providers;

        public GetSsoSettingsQueryHandler(ISsoProviderService providers) => _providers = providers;

        public async Task<SsoProviderDto> Handle(GetSsoSettingsQuery request, CancellationToken cancellationToken)
        {
            var p = await _providers.GetProviderAsync();
            if (p == null)
                return new SsoProviderDto { Type = string.Empty };

            var dto = new SsoProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type.ToString(),
                IsEnabled = p.IsEnabled,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            };

            if (p.Type == SsoProviderType.Oidc)
            {
                var c = await _providers.GetDecryptedOidcAsync();
                if (c != null)
                {
                    dto.Oidc = new OidcConfigViewDto
                    {
                        Authority = c.Authority,
                        ClientId = c.ClientId,
                        Scopes = c.Scopes,
                        SubjectClaim = c.SubjectClaim,
                        EmailClaim = c.EmailClaim,
                        NameClaim = c.NameClaim,
                        HasClientSecret = !string.IsNullOrEmpty(c.ClientSecret),
                    };
                }
            }
            else if (p.Type == SsoProviderType.Saml)
            {
                var c = await _providers.GetDecryptedSamlAsync();
                if (c != null)
                {
                    dto.Saml = new SamlConfigViewDto
                    {
                        SpEntityId = c.SpEntityId,
                        IdpEntityId = c.IdpEntityId,
                        IdpSsoUrl = c.IdpSsoUrl,
                        IdpCertificate = c.IdpCertificate,
                        SpCertificate = c.SpCertificate,
                        NameIdFormat = c.NameIdFormat,
                        AttributeEmail = c.AttributeEmail,
                        AttributeName = c.AttributeName,
                        WantAssertionsSigned = c.WantAssertionsSigned,
                        WantResponseSigned = c.WantResponseSigned,
                        HasSpPrivateKey = !string.IsNullOrEmpty(c.SpPrivateKey),
                    };
                }
            }

            return dto;
        }
    }
}
