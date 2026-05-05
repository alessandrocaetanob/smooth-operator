using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Auth.Queries
{
    public sealed record SmtpAvailableQuery : IRequest<SmtpAvailableResult>;

    public sealed record SmtpAvailableResult(bool Available);

    public sealed class SmtpAvailableQueryHandler : IRequestHandler<SmtpAvailableQuery, SmtpAvailableResult>
    {
        private readonly IEmailService _emailService;

        public SmtpAvailableQueryHandler(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task<SmtpAvailableResult> Handle(SmtpAvailableQuery request, CancellationToken cancellationToken)
        {
            var available = await _emailService.IsConfiguredAsync();
            return new SmtpAvailableResult(available);
        }
    }
}
