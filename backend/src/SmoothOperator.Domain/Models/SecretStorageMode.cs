namespace SmoothOperator.Domain.Models
{
    public enum SecretStorageMode
    {
        /// <summary>Secret is stored locally in the app DB, AES-256 encrypted.</summary>
        Local = 0,

        /// <summary>Secret lives in an external provider; only a reference is stored.</summary>
        External = 1
    }
}
