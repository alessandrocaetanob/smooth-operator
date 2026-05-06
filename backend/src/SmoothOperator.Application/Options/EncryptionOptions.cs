using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed encryption configuration.
/// Bound from the "Encryption" section in appsettings / environment variables (Encryption__Key).
/// </summary>
public sealed class EncryptionOptions : IValidatableObject
{
    public const string SectionName = "Encryption";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Encryption:Key is required. Set Encryption__Key in your environment.")]
    public string Key { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Key.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(Key, "^[0-9a-fA-F]+$",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100)))
            yield return new ValidationResult(
                "Encryption:Key must be a 64-character hex string (32 bytes for AES-256).",
                [nameof(Key)]);
    }
}
