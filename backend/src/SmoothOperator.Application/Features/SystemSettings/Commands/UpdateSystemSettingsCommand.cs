using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.SystemSettings.Commands
{
    public sealed record UpdateSystemSettingsCommand(UpdateSystemSettingsRequest Request) : IRequest<SystemSettingsDto>;

    public sealed class UpdateSystemSettingsCommandHandler
        : IRequestHandler<UpdateSystemSettingsCommand, SystemSettingsDto>
    {
        private static readonly Guid SingletonId = new("22222222-2222-2222-2222-222222222222");

        private readonly IAppDbContext _db;
        private readonly IAuditService _audit;

        public UpdateSystemSettingsCommandHandler(IAppDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<SystemSettingsDto> Handle(UpdateSystemSettingsCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;
            var s = await _db.SystemSettings.FirstOrDefaultAsync(cancellationToken);
            var creating = s == null;
            if (s == null)
            {
                s = new Domain.Models.SystemSettings { Id = SingletonId };
                _db.SystemSettings.Add(s);
            }

            s.AuditLogRetentionDays = request.AuditLogRetentionDays;
            s.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                creating ? "system_settings.created" : "system_settings.updated",
                "SystemSettings",
                s.Id.ToString(),
                new { s.AuditLogRetentionDays });

            return Queries.GetSystemSettingsQueryHandler.ToDto(s);
        }
    }
}
