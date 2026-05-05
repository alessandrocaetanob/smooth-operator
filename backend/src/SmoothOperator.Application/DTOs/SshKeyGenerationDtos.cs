using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class GenerateSshKeyRequest
    {
        [Required]
        [RegularExpression("^(rsa|ecdsa)$", ErrorMessage = "Key type must be one of: rsa, ecdsa")]
        public string KeyType { get; set; } = "rsa";
    }

    public class GenerateSshKeyResponse
    {
        public string PrivateKey { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
    }
}
