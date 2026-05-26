using SmoothOperator.Application.Interfaces;
using StackExchange.Redis;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Bundles cross-cutting services consumed by <see cref="GuacamoleProxyService"/>.
    /// Keeps the proxy ctor under SonarQube's 7-parameter ceiling without spreading
    /// the resolution logic across the surface.
    /// </summary>
    public sealed class GuacamoleProxyDependencies
    {
        public IEncryptionService Encryption { get; }
        public IConnectionMultiplexer Redis { get; }
        public IAppMetrics Metrics { get; }
        public ISecretProviderFactory SecretProviderFactory { get; }

        public GuacamoleProxyDependencies(
            IEncryptionService encryption,
            IConnectionMultiplexer redis,
            IAppMetrics metrics,
            ISecretProviderFactory secretProviderFactory)
        {
            Encryption = encryption;
            Redis = redis;
            Metrics = metrics;
            SecretProviderFactory = secretProviderFactory;
        }
    }
}
