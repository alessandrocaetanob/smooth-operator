using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Mfa.Commands
{
    public sealed record ConfirmTotpEnrollmentCommand(Guid UserId, string Code) : IRequest<ConfirmTotpResult>;

    public sealed record ConfirmTotpResult(IReadOnlyList<string> RecoveryCodes);

    public sealed class ConfirmTotpEnrollmentCommandHandler : IRequestHandler<ConfirmTotpEnrollmentCommand, ConfirmTotpResult>
    {
        private const int RecoveryCodeCount = 10;
        private const int RecoveryCodeLength = 8;

        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IMfaService _mfaService;
        private readonly IAuditService _audit;

        public ConfirmTotpEnrollmentCommandHandler(
            IAppDbContext context,
            IEncryptionService encryptionService,
            IMfaService mfaService,
            IAuditService audit)
        {
            _context = context;
            _encryptionService = encryptionService;
            _mfaService = mfaService;
            _audit = audit;
        }

        public async Task<ConfirmTotpResult> Handle(ConfirmTotpEnrollmentCommand request, CancellationToken cancellationToken)
        {
            var credential = await _context.MfaCredentials
                .FirstOrDefaultAsync(m => m.UserId == request.UserId && !m.IsEnabled, cancellationToken)
                ?? throw new NotFoundException("No pending MFA enrollment found. Start enrollment first.");

            var secret = _encryptionService.Decrypt(credential.EncryptedSecret);
            if (!_mfaService.VerifyTotp(secret, request.Code))
                throw new UnauthorizedException("Invalid verification code.");

            credential.IsEnabled = true;
            credential.EnabledAt = DateTime.UtcNow;

            // Generate recovery codes, store hashes, return plaintext once
            var plainCodes = new List<string>(RecoveryCodeCount);
            for (var i = 0; i < RecoveryCodeCount; i++)
            {
                var plain = GenerateRecoveryCode();
                plainCodes.Add(plain);
                _context.MfaRecoveryCodes.Add(new MfaRecoveryCode
                {
                    Id = Guid.NewGuid(),
                    MfaCredentialId = credential.Id,
                    CodeHash = BCrypt.Net.BCrypt.HashPassword(plain),
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("user.mfa_enabled", "User", request.UserId.ToString(), new { type = "totp" });

            return new ConfirmTotpResult(plainCodes);
        }

        private static string GenerateRecoveryCode()
        {
            var bytes = new byte[RecoveryCodeLength];
            RandomNumberGenerator.Fill(bytes);
            // Produce uppercase hex pairs, e.g. "A3F9-2B17-..."
            var hex = Convert.ToHexString(bytes);
            return $"{hex[..4]}-{hex[4..8]}";
        }
    }
}
