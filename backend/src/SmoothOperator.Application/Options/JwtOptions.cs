using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed JWT configuration.
/// Bound from the "Jwt" section in appsettings / environment variables (Jwt__Key, etc.).
/// </summary>
public sealed class JwtOptions : IValidatableObject
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:Key is required. Set Jwt__Key in your environment.")]
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "smooth-operator";

    public string Audience { get; set; } = "smooth-operator-api";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(Key) < 32)
            yield return new ValidationResult(
                "Jwt:Key must be at least 32 bytes (UTF-8) for HS256.",
                [nameof(Key)]);
    }
}
