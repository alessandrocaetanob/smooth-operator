using System;
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
    public sealed record EnrollTotpCommand(Guid UserId) : IRequest<EnrollTotpResult>;

    public sealed record EnrollTotpResult(string SecretBase32, string OtpAuthUri);

    public sealed class EnrollTotpCommandHandler : IRequestHandler<EnrollTotpCommand, EnrollTotpResult>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public EnrollTotpCommandHandler(IAppDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task<EnrollTotpResult> Handle(EnrollTotpCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            // Remove any pending (not yet confirmed) credential before starting fresh
            var existing = await _context.MfaCredentials
                .FirstOrDefaultAsync(m => m.UserId == request.UserId, cancellationToken);

            if (existing is { IsEnabled: true })
                throw new ConflictException("MFA is already enabled. Disable it before re-enrolling.");

            if (existing is { IsEnabled: false })
                _context.MfaCredentials.Remove(existing);

            // Generate a random 20-byte TOTP secret
            var secretBytes = new byte[20];
            RandomNumberGenerator.Fill(secretBytes);
            var secretBase32 = Base32Encode(secretBytes);

            var credential = new MfaCredential
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                EncryptedSecret = _encryptionService.Encrypt(secretBase32),
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.MfaCredentials.Add(credential);
            await _context.SaveChangesAsync(cancellationToken);

            var issuer = Uri.EscapeDataString("Smooth Operator");
            var account = Uri.EscapeDataString(user.Email);
            var uri = $"otpauth://totp/{issuer}:{account}?secret={secretBase32}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

            return new EnrollTotpResult(secretBase32, uri);
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var result = new System.Text.StringBuilder();
            int buffer = 0, bitsLeft = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    result.Append(alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                    bitsLeft -= 5;
                }
            }
            if (bitsLeft > 0)
                result.Append(alphabet[(buffer << (5 - bitsLeft)) & 31]);
            return result.ToString();
        }
    }
}
