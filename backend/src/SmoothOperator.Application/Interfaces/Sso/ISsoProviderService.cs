using System.Threading.Tasks;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces.Sso
{
    public interface ISsoProviderService
    {
        Task<SsoProvider?> GetActiveProviderAsync();
        Task<SsoProvider?> GetProviderAsync();
        Task<OidcConfig?> GetDecryptedOidcAsync();
        Task<SamlConfig?> GetDecryptedSamlAsync();
        Task<SsoProvider> UpsertOidcAsync(string name, OidcConfig config);
        Task<SsoProvider> UpsertSamlAsync(string name, SamlConfig config);
        Task<bool> DeleteAsync();
        Task<SsoProvider?> SetEnabledAsync(bool enabled);
    }
}
