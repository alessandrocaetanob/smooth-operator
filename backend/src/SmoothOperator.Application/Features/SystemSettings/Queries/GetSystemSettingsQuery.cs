using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.SystemSettings.Queries
{
    public sealed record GetSystemSettingsQuery : IRequest<SystemSettingsDto>;

    public sealed class GetSystemSettingsQueryHandler : IRequestHandler<GetSystemSettingsQuery, SystemSettingsDto>
    {
        private readonly IAppDbContext _db;

        public GetSystemSettingsQueryHandler(IAppDbContext db) => _db = db;

        public async Task<SystemSettingsDto> Handle(GetSystemSettingsQuery request, CancellationToken cancellationToken)
        {
            var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

            if (s == null)
                return new SystemSettingsDto { AuditLogRetentionDays = 0, UpdatedAt = DateTime.UtcNow };

            return ToDto(s);
        }

        internal static SystemSettingsDto ToDto(Domain.Models.SystemSettings s) => new()
        {
            AuditLogRetentionDays = s.AuditLogRetentionDays,
            UpdatedAt = s.UpdatedAt,
        };
    }
}
