namespace SmoothOperator.Application.Options;

/// <summary>
/// Strongly-typed OpenTelemetry exporter configuration.
/// Bound from the "Otel" section in appsettings / environment variables (Otel__Endpoint, Otel__ServiceName).
/// </summary>
public sealed class OtelOptions
{
    public const string SectionName = "Otel";

    /// <summary>OTLP gRPC endpoint for Tempo / collector. Null/empty disables tracing export.</summary>
    public string? Endpoint { get; set; }

    public string ServiceName { get; set; } = "smooth-operator-backend";
}
