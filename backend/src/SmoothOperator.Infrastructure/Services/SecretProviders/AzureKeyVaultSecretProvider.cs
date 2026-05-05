using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Infrastructure.Services.SecretProviders
{
    /// <summary>
    /// Azure Key Vault implementation of <see cref="ISecretProvider"/> using Service Principal auth.
    /// </summary>
    public sealed class AzureKeyVaultSecretProvider : ISecretProvider
    {
        private readonly SecretClient _client;

        public AzureKeyVaultSecretProvider(AzureKeyVaultConfig config)
        {
            var credential = new ClientSecretCredential(
                config.TenantId,
                config.ClientId,
                config.ClientSecret);

            _client = new SecretClient(new Uri(config.VaultUri), credential);
        }

        public async Task<string> GetSecretAsync(
            string secretName,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _client.GetSecretAsync(secretName, version, cancellationToken);
                return response.Value.Value;
            }
            catch (AuthenticationFailedException ex)
            {
                throw new SecretProviderException(
                    "vault_auth_failed",
                    "Vault authentication failed — check service principal credentials.",
                    ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new SecretProviderException(
                    "secret_not_found",
                    $"Secret '{secretName}' not found in vault.",
                    ex);
            }
            catch (RequestFailedException ex) when (ex.Status is 401 or 403)
            {
                throw new SecretProviderException(
                    "vault_access_denied",
                    "Vault access denied — verify Key Vault RBAC permissions for the service principal.",
                    ex);
            }
        }

        public async Task<string> SetSecretAsync(
            string secretName,
            string secretValue,
            CancellationToken cancellationToken = default)
        {
            var response = await _client.SetSecretAsync(secretName, secretValue, cancellationToken);
            return response.Value.Properties.Version ?? string.Empty;
        }

        public async Task<IReadOnlyList<string>> ListSecretNamesAsync(
            CancellationToken cancellationToken = default)
        {
            var names = new List<string>();
            await foreach (var props in _client.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                if (props.Enabled == true)
                    names.Add(props.Name);
            }
            return names.AsReadOnly();
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            // Listing secret properties is a cheap, non-destructive way to verify auth + network.
            await foreach (var _ in _client.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                break;
            }
            return true;
        }
    }
}
