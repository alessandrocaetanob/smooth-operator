using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.Features.Recordings.Commands
{
    public sealed record DeleteRecordingCommand(Guid RecordingId) : IRequest<Unit>;

    public sealed class DeleteRecordingCommandHandler : IRequestHandler<DeleteRecordingCommand, Unit>
    {
        private readonly IAppDbContext _db;
        private readonly IRecordingStorageFactory _storageFactory;
        private readonly IAuditService _audit;

        public DeleteRecordingCommandHandler(
            IAppDbContext db,
            IRecordingStorageFactory storageFactory,
            IAuditService audit)
        {
            _db = db;
            _storageFactory = storageFactory;
            _audit = audit;
        }

        public async Task<Unit> Handle(DeleteRecordingCommand request, CancellationToken cancellationToken)
        {
            var recording = await _db.Recordings.FirstOrDefaultAsync(r => r.Id == request.RecordingId, cancellationToken)
                ?? throw new NotFoundException("Recording not found.");

            if (recording.Status == RecordingStatus.Available || recording.Status == RecordingStatus.Failed)
            {
                try
                {
                    var storage = await _storageFactory.CreateAsync(cancellationToken);
                    await storage.DeleteAsync(recording.StorageKey, cancellationToken);
                }
                catch
                {
                    // Storage delete failed — DB row still purged. Admins can clean up orphaned blobs by hand.
                }
            }

            _db.Recordings.Remove(recording);
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("recording.deleted", "Recording", recording.Id.ToString(),
                new { recording.SessionId, recording.ConnectionId, recording.StorageKey });

            return Unit.Value;
        }
    }
}
