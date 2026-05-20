using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Helpers;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Auth.Commands
{
    public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

    public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
    {
        private const string ProviderLocal = "local";
        private const string OutcomeFailure = "failure";

        private readonly IAppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IAuditService _audit;
        private readonly IAppMetrics _metrics;

        public LoginCommandHandler(
            IAppDbContext context,
            ITokenService tokenService,
            IAuditService audit,
            IAppMetrics metrics)
        {
            _context = context;
            _tokenService = tokenService;
            _audit = audit;
            _metrics = metrics;
        }

        public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            const string invalid = "Invalid email or password.";

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                await _audit.WriteAsync("user.login_failed", "User", string.Empty,
                    new { provider = ProviderLocal, email, reason = "user_not_found_or_no_password" }, outcome: OutcomeFailure);
                _metrics.RecordLoginAttempt(OutcomeFailure);
                throw new UnauthorizedException(invalid);
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                await _audit.WriteAsync("user.login_failed", "User", user.Id.ToString(),
                    new { provider = ProviderLocal }, outcome: OutcomeFailure);
                _metrics.RecordLoginAttempt(OutcomeFailure);
                throw new UnauthorizedException(invalid);
            }

            if (!user.IsActive)
            {
                await _audit.WriteAsync("user.login_failed", "User", user.Id.ToString(),
                    new { provider = ProviderLocal, reason = "user_disabled" }, outcome: OutcomeFailure);
                _metrics.RecordLoginAttempt(OutcomeFailure);
                throw new ForbiddenException("User account is disabled.");
            }

            // Check if MFA is enabled for this user
            var hasMfa = await _context.MfaCredentials
                .AsNoTracking()
                .AnyAsync(m => m.UserId == user.Id && m.IsEnabled, cancellationToken);

            if (hasMfa)
            {
                // Return a short-lived challenge token; the full JWT is issued after MFA verification
                var challengeToken = _tokenService.CreateMfaChallengeToken(user.Id);
                return new LoginResult { MfaRequired = true, ChallengeToken = challengeToken };
            }

            await _audit.WriteAsync("user.login", "User", user.Id.ToString(),
                new { provider = ProviderLocal });
            _metrics.RecordLoginAttempt("success");

            return new LoginResult { Auth = UserHelper.ToAuthResponse(user, _tokenService) };
        }
    }
}
