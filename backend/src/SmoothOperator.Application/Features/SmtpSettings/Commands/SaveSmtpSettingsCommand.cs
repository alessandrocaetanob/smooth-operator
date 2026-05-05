using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.SmtpSettings.Commands
{
    public sealed record SaveSmtpSettingsCommand(UpdateSmtpSettingsRequest Request) : IRequest<SmtpSettingsDto>;

    public sealed class SaveSmtpSettingsCommandHandler : IRequestHandler<SaveSmtpSettingsCommand, SmtpSettingsDto>
    {
        private static readonly Guid SingletonId = new("11111111-1111-1111-1111-111111111111");

        private readonly IAppDbContext _db;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;

        public SaveSmtpSettingsCommandHandler(
            IAppDbContext db,
            IEncryptionService encryption,
            IAuditService audit)
        {
            _db = db;
            _encryption = encryption;
            _audit = audit;
        }

        public async Task<SmtpSettingsDto> Handle(SaveSmtpSettingsCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;
            var s = await _db.SmtpSettings.FirstOrDefaultAsync(cancellationToken);
            var creating = s == null;
            if (s == null)
            {
                s = new SmoothOperator.Domain.Models.SmtpSettings { Id = SingletonId };
                _db.SmtpSettings.Add(s);
            }

            s.Host = request.Host.Trim();
            s.Port = request.Port;
            s.UseSsl = request.UseSsl;
            s.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
            s.FromAddress = request.FromAddress.Trim();
            s.FromName = string.IsNullOrWhiteSpace(request.FromName) ? null : request.FromName.Trim();
            s.Enabled = request.Enabled;
            s.UpdatedAt = DateTime.UtcNow;

            // Password handling: null => keep existing; "" => clear; non-empty => replace.
            if (request.Password != null)
            {
                if (request.Password.Length == 0)
                    s.EncryptedPassword = null;
                else
                    s.EncryptedPassword = _encryption.Encrypt(request.Password);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(creating ? "smtp.created" : "smtp.updated", "SmtpSettings", s.Id.ToString(),
                new { s.Host, s.Port, s.UseSsl, s.FromAddress, s.Enabled });

            return Queries.GetSmtpSettingsQueryHandler.ToDto(s);
        }
    }
}
