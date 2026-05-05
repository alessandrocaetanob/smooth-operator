using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed Apache Guacamole daemon (guacd) configuration.
/// Bound from the "Guacd" section in appsettings / environment variables (Guacd__Host, Guacd__Port).
/// </summary>
public sealed class GuacdOptions
{
    public const string SectionName = "Guacd";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = "guacd";

    [Range(1, 65535)]
    public int Port { get; set; } = 4822;
}
