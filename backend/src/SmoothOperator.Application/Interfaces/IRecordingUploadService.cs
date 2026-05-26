using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// Moves the temp <c>.guac</c> file written by guacd into the configured
    /// <see cref="IRecordingStorageService"/> and finalises the <c>Recording</c> row.
    /// Invoked from <c>GuacamoleProxyService</c> after a session ends.
    /// </summary>
    public interface IRecordingUploadService
    {
        Task UploadAndFinaliseAsync(Guid recordingId, string localFilePath, CancellationToken cancellationToken);
    }
}
