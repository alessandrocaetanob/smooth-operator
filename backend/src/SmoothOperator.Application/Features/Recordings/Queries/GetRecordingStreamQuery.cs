using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.Features.Recordings.Queries
{
    public sealed record GetRecordingStreamQuery(ClaimsPrincipal User, Guid RecordingId)
        : IRequest<RecordingStreamResult>;

    public sealed record RecordingStreamResult(
        Stream Content,
        string FileName,
        long? SizeBytes);

    public sealed class GetRecordingStreamQueryHandler
        : IRequestHandler<GetRecordingStreamQuery, RecordingStreamResult>
    {
        private readonly IAppDbContext _db;
        private readonly IAccessControlService _access;
        private readonly IRecordingStorageFactory _storageFactory;

        public GetRecordingStreamQueryHandler(
            IAppDbContext db,
            IAccessControlService access,
            IRecordingStorageFactory storageFactory)
        {
            _db = db;
            _access = access;
            _storageFactory = storageFactory;
        }

        public async Task<RecordingStreamResult> Handle(GetRecordingStreamQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User)
                ?? throw new UnauthorizedException("User profile not found.");

            var recording = await _db.Recordings.AsNoTracking()
                .Include(r => r.Connection!).ThenInclude(c => c.ConnectionGroup)
                .Include(r => r.Connection!).ThenInclude(c => c.Users)
                .FirstOrDefaultAsync(r => r.Id == request.RecordingId, cancellationToken);

            // Return 404 (not 403) for both missing-and-unauthorized so we don't leak existence.
            if (recording == null || recording.Connection == null)
                throw new NotFoundException("Recording not found.");
            if (recording.Status != RecordingStatus.Available)
                throw new NotFoundException("Recording not available.");

            if (!_access.CanUseConnection(profile, recording.Connection))
                throw new NotFoundException("Recording not found.");

            var storage = await _storageFactory.CreateAsync(cancellationToken);
            var stream = await storage.OpenReadAsync(recording.StorageKey, cancellationToken);

            var fileName = $"{recording.SessionId}.guac";
            return new RecordingStreamResult(stream, fileName, recording.FileSizeBytes);
        }
    }
}
