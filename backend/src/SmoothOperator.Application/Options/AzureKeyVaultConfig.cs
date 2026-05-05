using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.Options
{
    /// <summary>
    /// Binding shape for the per-provider EncryptedConfigJson payload when Type == AzureKeyVault.
    /// Deserialized from the decrypted JSON; never stored in plain form.
    /// </summary>
    public sealed class AzureKeyVaultConfig
    {
        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        public string ClientSecret { get; set; } = string.Empty;

        [Required]
        public string VaultUri { get; set; } = string.Empty;
    }
}
