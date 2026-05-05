using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmoothOperator.Application.Interfaces
{
    /// <summary>
    /// Abstraction over an external secret management system (Azure Key Vault, AWS Secrets Manager, etc.).
    /// </summary>
    public interface ISecretProvider
    {
        /// <summary>Retrieves the value of a secret by name. If version is null, the latest version is returned.</summary>
        Task<string> GetSecretAsync(string secretName, string? version = null, CancellationToken cancellationToken = default);

        /// <summary>Creates or updates a secret in the external vault and returns the version identifier.</summary>
        Task<string> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);

        /// <summary>Lists the names of all secrets in the vault (names only, no values).</summary>
        Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default);

        /// <summary>Validates connectivity and credentials. Returns true on success; throws on failure with a descriptive message.</summary>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}
