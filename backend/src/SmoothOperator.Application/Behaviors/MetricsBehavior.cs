using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Behaviors
{
    /// <summary>
    /// Records a Prometheus duration histogram (and, via its implicit _count, a
    /// request counter) for every MediatR command/query, labelled by request type
    /// and success/failure outcome.
    /// </summary>
    public sealed class MetricsBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IAppMetrics _metrics;

        public MetricsBehavior(IAppMetrics metrics)
        {
            _metrics = metrics;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await next(cancellationToken);
                stopwatch.Stop();
                _metrics.RecordMediatrRequest(requestName, "success", stopwatch.Elapsed.TotalSeconds);
                return response;
            }
            catch
            {
                stopwatch.Stop();
                _metrics.RecordMediatrRequest(requestName, "failure", stopwatch.Elapsed.TotalSeconds);
                throw;
            }
        }
    }
}
