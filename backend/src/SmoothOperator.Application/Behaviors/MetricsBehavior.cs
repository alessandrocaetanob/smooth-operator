using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace SmoothOperator.Application.Behaviors
{
    public sealed class MetricsBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            return await next(cancellationToken);
        }
    }
}
