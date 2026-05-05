using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// Resolves the correct <see cref="ISecretProvider"/> implementation for a given <see cref="SecretProvider"/> entity.
    /// </summary>
    public interface ISecretProviderFactory
    {
        ISecretProvider Create(SecretProvider provider);
    }
}
