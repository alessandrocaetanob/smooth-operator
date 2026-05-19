using System;
using System.Text.Json;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Infrastructure.Services.SecretProviders
{
    public sealed class SecretProviderFactory : ISecretProviderFactory
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IEncryptionService _encryption;

        public SecretProviderFactory(IEncryptionService encryption)
        {
            _encryption = encryption;
        }

        public ISecretProvider Create(SecretProvider provider)
        {
            if (!provider.IsEnabled)
                throw new InvalidOperationException($"Secret provider '{provider.Name}' is disabled.");

            var json = _encryption.Decrypt(provider.EncryptedConfigJson);

            return provider.Type switch
            {
                SecretProviderType.AzureKeyVault => CreateAzureKeyVault(json),
                _ => throw new NotSupportedException($"Secret provider type '{provider.Type}' is not supported.")
            };
        }

        private static AzureKeyVaultSecretProvider CreateAzureKeyVault(string configJson)
        {
            var config = JsonSerializer.Deserialize<AzureKeyVaultConfig>(configJson, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Azure Key Vault configuration.");

            return new AzureKeyVaultSecretProvider(config);
        }
    }
}
