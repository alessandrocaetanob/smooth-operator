using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs
{
    public class GenerateSshKeyRequest
    {
        [Required]
        [RegularExpression("^(rsa|ecdsa|ed25519)$", ErrorMessage = "Key type must be one of: rsa, ecdsa, ed25519")]
        public string KeyType { get; set; } = "rsa";
    }

    public class GenerateSshKeyResponse
    {
        public string PrivateKey { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
    }
}
