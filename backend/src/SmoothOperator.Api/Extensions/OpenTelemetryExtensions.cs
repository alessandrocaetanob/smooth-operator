using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace SmoothOperator.Api.Extensions
{
    internal static class OpenTelemetryExtensions
    {
        internal static void AddSmoothOperatorOpenTelemetry(this WebApplicationBuilder builder)
        {
            var otelSection = builder.Configuration.GetSection("Otel");
            var otelEndpoint = otelSection["Endpoint"];
            var otelServiceName = otelSection["ServiceName"] ?? "smooth-operator-backend";

            if (string.IsNullOrWhiteSpace(otelEndpoint))
                return;

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(otelServiceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));
        }
    }
}
