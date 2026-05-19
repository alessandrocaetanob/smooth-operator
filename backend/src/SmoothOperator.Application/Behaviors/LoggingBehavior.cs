using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Application.Behaviors
{
    public sealed class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);

            var response = await next(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);

            return response;
        }
    }
}
