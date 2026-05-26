using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.Features.Recordings.Queries
{
    public sealed record GetRecordingsQuery(
        ClaimsPrincipal User,
        Guid? UserId,
        Guid? ConnectionId,
        Guid? VaultId,
        DateTime? From,
        DateTime? To,
        RecordingStatus? Status,
        int Page,
        int PageSize) : IRequest<RecordingsListDto>;

    public sealed class GetRecordingsQueryHandler : IRequestHandler<GetRecordingsQuery, RecordingsListDto>
    {
        private readonly IAppDbContext _db;
        private readonly IAccessControlService _access;

        public GetRecordingsQueryHandler(IAppDbContext db, IAccessControlService access)
        {
            _db = db;
            _access = access;
        }

        private static DateTime EnsureUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        public async Task<RecordingsListDto> Handle(GetRecordingsQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User)
                ?? throw new UnauthorizedException("User profile not found.");

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 200);

            // Restrict recordings to connections the caller can see.
            var allowedConnectionIds = _access
                .ApplyConnectionScope(_db.Connections.AsNoTracking(), profile)
                .Select(c => c.Id);

            var query = _db.Recordings.AsNoTracking()
                .Include(r => r.Connection!).ThenInclude(c => c.ConnectionGroup)
                .Include(r => r.User)
                .Where(r => allowedConnectionIds.Contains(r.ConnectionId));

            if (request.UserId.HasValue)
                query = query.Where(r => r.UserId == request.UserId.Value);
            if (request.ConnectionId.HasValue)
                query = query.Where(r => r.ConnectionId == request.ConnectionId.Value);
            if (request.VaultId.HasValue)
                query = query.Where(r => r.Connection!.ConnectionGroupId == request.VaultId.Value);
            if (request.From.HasValue)
            {
                // Npgsql refuses Unspecified kinds against 'timestamp with time zone'.
                // The frontend uses an HTML <input type="datetime-local"> which serialises as a
                // zone-less ISO string ("2026-05-25T14:30:00") — we treat that as UTC.
                var fromUtc = EnsureUtc(request.From.Value);
                query = query.Where(r => r.StartedAt >= fromUtc);
            }
            if (request.To.HasValue)
            {
                var toUtc = EnsureUtc(request.To.Value);
                query = query.Where(r => r.StartedAt < toUtc);
            }
            if (request.Status.HasValue)
                query = query.Where(r => r.Status == request.Status.Value);

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(r => r.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new RecordingsListDto
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items.ConvertAll(r => new RecordingDto
                {
                    Id = r.Id,
                    SessionId = r.SessionId,
                    ConnectionId = r.ConnectionId,
                    ConnectionName = r.Connection?.Name ?? string.Empty,
                    Protocol = r.Connection?.Protocol ?? string.Empty,
                    VaultId = r.Connection?.ConnectionGroupId,
                    VaultName = r.Connection?.ConnectionGroup?.Name,
                    UserId = r.UserId,
                    UserEmail = r.User?.Email,
                    StartedAt = r.StartedAt,
                    EndedAt = r.EndedAt,
                    DurationSeconds = r.DurationSeconds,
                    StorageType = r.StorageType,
                    FileSizeBytes = r.FileSizeBytes,
                    Status = r.Status,
                    ErrorMessage = r.ErrorMessage,
                    IncludeKeys = r.IncludeKeys,
                }),
            };
        }
    }
}
