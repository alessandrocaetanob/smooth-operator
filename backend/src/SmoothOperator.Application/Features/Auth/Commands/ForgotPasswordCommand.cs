using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Auth.Commands
{
    public sealed record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResult>;

    public sealed record ForgotPasswordResult(string Message);

    public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
    {
        private readonly IAppDbContext _context;
        private readonly IInviteService _inviteService;
        private readonly IEmailService _emailService;
        private readonly IAuditService _audit;

        public ForgotPasswordCommandHandler(
            IAppDbContext context,
            IInviteService inviteService,
            IEmailService emailService,
            IAuditService audit)
        {
            _context = context;
            _inviteService = inviteService;
            _emailService = emailService;
            _audit = audit;
        }

        public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            // Always succeed to avoid email enumeration.
            if (user != null && await _emailService.IsConfiguredAsync())
            {
                var (_, _, resetUrl) = await _inviteService.CreateAsync(
                    user.Id, IInviteService.TypePasswordReset, System.TimeSpan.FromHours(2), null);
                await _emailService.SendPasswordResetAsync(user.Email, user.Name, resetUrl);
                await _audit.WriteAsync("password.reset_requested", "User", user.Id.ToString(),
                    new { provider = "local" });
            }

            return new ForgotPasswordResult("If the account exists, a reset link has been sent.");
        }
    }
}
