using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.SmtpSettings.Queries
{
    public sealed record GetSmtpSettingsQuery : IRequest<SmtpSettingsDto>;

    public sealed class GetSmtpSettingsQueryHandler : IRequestHandler<GetSmtpSettingsQuery, SmtpSettingsDto>
    {
        private readonly IAppDbContext _db;

        public GetSmtpSettingsQueryHandler(IAppDbContext db) => _db = db;

        public async Task<SmtpSettingsDto> Handle(GetSmtpSettingsQuery request, CancellationToken cancellationToken)
        {
            var s = await _db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            return ToDto(s);
        }

        internal static SmtpSettingsDto ToDto(SmoothOperator.Domain.Models.SmtpSettings? s)
        {
            if (s == null)
                return new SmtpSettingsDto { Configured = false };

            return new SmtpSettingsDto
            {
                Configured = !string.IsNullOrWhiteSpace(s.Host) && !string.IsNullOrWhiteSpace(s.FromAddress),
                Host = s.Host,
                Port = s.Port,
                UseSsl = s.UseSsl,
                Username = s.Username,
                FromAddress = s.FromAddress,
                FromName = s.FromName,
                Enabled = s.Enabled,
                HasPassword = !string.IsNullOrEmpty(s.EncryptedPassword),
                UpdatedAt = s.UpdatedAt,
            };
        }
    }
}
