using System.IO;
using System.Threading.Tasks;
using Prometheus;
using SmoothOperator.Infrastructure.Services;
using Xunit;

namespace SmoothOperator.Infrastructure.Tests.Services;

public class AppMetricsTests
{
    [Fact]
    public async Task RecordMediatrRequest_ExportsHistogramSeriesForBothOutcomes()
    {
        var metrics = new AppMetrics();

        metrics.RecordMediatrRequest("SampleQuery", "success", 0.012);
        metrics.RecordMediatrRequest("SampleQuery", "failure", 0.034);

        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        stream.Position = 0;
        var exported = await new StreamReader(stream).ReadToEndAsync();

        Assert.Contains("smooth_operator_mediatr_request_duration_seconds", exported);
        Assert.Contains("request=\"SampleQuery\"", exported);
        Assert.Contains("outcome=\"success\"", exported);
        Assert.Contains("outcome=\"failure\"", exported);
    }
}
