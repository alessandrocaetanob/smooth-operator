using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.SmtpSettings.Commands
{
    public sealed record SendSmtpTestCommand(string ToEmail) : IRequest<EmailSendResult>;

    public sealed class SendSmtpTestCommandHandler : IRequestHandler<SendSmtpTestCommand, EmailSendResult>
    {
        private readonly IEmailService _email;
        private readonly IAuditService _audit;

        public SendSmtpTestCommandHandler(IEmailService email, IAuditService audit)
        {
            _email = email;
            _audit = audit;
        }

        public async Task<EmailSendResult> Handle(SendSmtpTestCommand request, CancellationToken cancellationToken)
        {
            if (!await _email.IsConfiguredAsync())
                return new EmailSendResult(false, "SMTP relay is not configured.");

            var result = await _email.SendTestAsync(request.ToEmail);
            await _audit.WriteAsync("smtp.test_sent", "SmtpSettings", "11111111-1111-1111-1111-111111111111",
                new { to = request.ToEmail, success = result.Success });

            return result;
        }
    }
}
