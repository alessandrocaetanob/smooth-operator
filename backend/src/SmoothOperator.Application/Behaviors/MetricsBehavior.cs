using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Behaviors
{
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
            return await next(cancellationToken);
        }
    }
}
