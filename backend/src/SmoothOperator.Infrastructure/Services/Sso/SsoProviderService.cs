using System;
using System.Threading.Tasks;
using System.Text.Json;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Infrastructure.Services.Sso
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

    public class SsoProviderService : ISsoProviderService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;

        public SsoProviderService(AppDbContext db, IEncryptionService encryption)
        {
            _db = db;
            _encryption = encryption;
        }

        public Task<SsoProvider?> GetActiveProviderAsync() =>
            _db.SsoProviders.AsNoTracking().FirstOrDefaultAsync(p => p.IsEnabled);

        public Task<SsoProvider?> GetProviderAsync() =>
            _db.SsoProviders.AsNoTracking().FirstOrDefaultAsync();

        public async Task<OidcConfig?> GetDecryptedOidcAsync()
        {
            var p = await _db.SsoProviders.AsNoTracking().FirstOrDefaultAsync();
            if (p == null || p.Type != SsoProviderType.Oidc) return null;
            return JsonSerializer.Deserialize<OidcConfig>(_encryption.Decrypt(p.EncryptedConfigJson), JsonOptions);
        }

        public async Task<SamlConfig?> GetDecryptedSamlAsync()
        {
            var p = await _db.SsoProviders.AsNoTracking().FirstOrDefaultAsync();
            if (p == null || p.Type != SsoProviderType.Saml) return null;
            return JsonSerializer.Deserialize<SamlConfig>(_encryption.Decrypt(p.EncryptedConfigJson), JsonOptions);
        }

        public Task<SsoProvider> UpsertOidcAsync(string name, OidcConfig config) =>
            UpsertInternalAsync(name, SsoProviderType.Oidc, JsonSerializer.Serialize(config, JsonOptions));

        public Task<SsoProvider> UpsertSamlAsync(string name, SamlConfig config) =>
            UpsertInternalAsync(name, SsoProviderType.Saml, JsonSerializer.Serialize(config, JsonOptions));

        private async Task<SsoProvider> UpsertInternalAsync(string name, SsoProviderType type, string json)
        {
            var encrypted = _encryption.Encrypt(json);
            var existing = await _db.SsoProviders.FirstOrDefaultAsync();
            if (existing == null)
            {
                existing = new SsoProvider
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Type = type,
                    IsEnabled = false,
                    EncryptedConfigJson = encrypted,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.SsoProviders.Add(existing);
            }
            else
            {
                existing.Name = name;
                existing.Type = type;
                existing.EncryptedConfigJson = encrypted;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync()
        {
            var existing = await _db.SsoProviders.FirstOrDefaultAsync();
            if (existing == null) return false;
            _db.SsoProviders.Remove(existing);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<SsoProvider?> SetEnabledAsync(bool enabled)
        {
            var existing = await _db.SsoProviders.FirstOrDefaultAsync();
            if (existing == null) return null;
            existing.IsEnabled = enabled;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }
    }
}
