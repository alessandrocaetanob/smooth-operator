using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.RecordingStorageSettings.Commands
{
    public sealed record TestRecordingStorageCommand : IRequest<RecordingStorageTestResult>;

    public sealed class TestRecordingStorageCommandHandler
        : IRequestHandler<TestRecordingStorageCommand, RecordingStorageTestResult>
    {
        private readonly IRecordingStorageFactory _factory;
        private readonly IAuditService _audit;

        public TestRecordingStorageCommandHandler(IRecordingStorageFactory factory, IAuditService audit)
        {
            _factory = factory;
            _audit = audit;
        }

        public async Task<RecordingStorageTestResult> Handle(
            TestRecordingStorageCommand request, CancellationToken cancellationToken)
        {
            var storage = await _factory.CreateAsync(cancellationToken);
            var error = await storage.TestConnectionAsync(cancellationToken);
            var success = error == null;

            await _audit.WriteAsync("recording.storage_test", "RecordingStorageSettings",
                resourceId: null, details: new { storageType = storage.StorageType.ToString(), success, error },
                outcome: success ? "success" : "failure");

            return new RecordingStorageTestResult { Success = success, Error = error };
        }
    }
}
