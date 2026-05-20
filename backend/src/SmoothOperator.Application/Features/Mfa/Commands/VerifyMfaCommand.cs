using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Helpers;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Mfa.Commands
{
    public sealed record VerifyMfaCommand(string ChallengeToken, string Code) : IRequest<AuthResponse>;

    public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, AuthResponse>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IMfaService _mfaService;
        private readonly ITokenService _tokenService;
        private readonly IAuditService _audit;
        private readonly IAppMetrics _metrics;

        public VerifyMfaCommandHandler(
            IAppDbContext context,
            IEncryptionService encryptionService,
            IMfaService mfaService,
            ITokenService tokenService,
            IAuditService audit,
            IAppMetrics metrics)
        {
            _context = context;
            _encryptionService = encryptionService;
            _mfaService = mfaService;
            _tokenService = tokenService;
            _audit = audit;
            _metrics = metrics;
        }

        public async Task<AuthResponse> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
        {
            const string invalid = "Invalid or expired verification code.";

            var userId = _tokenService.ValidateMfaChallengeToken(request.ChallengeToken)
                ?? throw new UnauthorizedException("Invalid or expired MFA challenge.");

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
                ?? throw new UnauthorizedException(invalid);

            var credential = await _context.MfaCredentials
                .Include(m => m.RecoveryCodes)
                .FirstOrDefaultAsync(m => m.UserId == userId && m.IsEnabled, cancellationToken)
                ?? throw new UnauthorizedException(invalid);

            // Distinguish TOTP code (6 digits) from recovery code (XXXX-XXXX format)
            var isRecoveryCode = request.Code.Contains('-');
            bool verified;

            if (isRecoveryCode)
            {
                verified = false;
                foreach (var rc in credential.RecoveryCodes)
                {
                    if (rc.IsUsed) continue;
                    if (!BCrypt.Net.BCrypt.Verify(request.Code, rc.CodeHash)) continue;
                    rc.IsUsed = true;
                    rc.UsedAt = DateTime.UtcNow;
                    verified = true;
                    break;
                }
                if (verified)
                    await _audit.WriteAsync("user.mfa_recovery_used", "User", userId.ToString(), new { });
            }
            else
            {
                var secret = _encryptionService.Decrypt(credential.EncryptedSecret);
                verified = _mfaService.VerifyTotp(secret, request.Code);
            }

            if (!verified)
            {
                _metrics.RecordLoginAttempt("failure");
                await _audit.WriteAsync("user.mfa_failed", "User", userId.ToString(),
                    new { type = isRecoveryCode ? "recovery" : "totp" }, outcome: "failure");
                throw new UnauthorizedException(invalid);
            }

            await _context.SaveChangesAsync(cancellationToken);
            _metrics.RecordLoginAttempt("success");
            await _audit.WriteAsync("user.login", "User", userId.ToString(), new { provider = "local", mfa = true });

            return UserHelper.ToAuthResponse(user, _tokenService);
        }
    }
}
