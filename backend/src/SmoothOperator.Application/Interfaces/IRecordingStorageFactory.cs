using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Domain.Enums;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// Resolves the active <see cref="IRecordingStorageService"/> from the current
    /// singleton <c>RecordingStorageSettings</c> row. Pattern mirrors
    /// <c>ISecretProviderFactory</c>.
    /// </summary>
    public interface IRecordingStorageFactory
    {
        /// <summary>Resolves the impl for the type currently configured in the settings row.</summary>
        Task<IRecordingStorageService> CreateAsync(CancellationToken cancellationToken = default);

        /// <summary>Resolves an impl for an arbitrary type (used by the "test" endpoint before saving).</summary>
        IRecordingStorageService CreateFor(RecordingStorageType storageType);
    }
}
