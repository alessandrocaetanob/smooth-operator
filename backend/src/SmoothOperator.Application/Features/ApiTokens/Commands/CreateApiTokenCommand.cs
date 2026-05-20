using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.ApiTokens.Commands
{
    public sealed record CreateApiTokenCommand(Guid UserId, string Name, DateTime? ExpiresAt) : IRequest<CreateApiTokenResult>;

    public sealed class CreateApiTokenCommandHandler : IRequestHandler<CreateApiTokenCommand, CreateApiTokenResult>
    {
        private const string Prefix = "sop_";
        private const int LookupBytes = 7;   // 7 bytes → 12 base32 chars (no padding via TrimEnd)
        private const int SecretBytes = 20;  // 20 bytes → 32 base32 chars

        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public CreateApiTokenCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<CreateApiTokenResult> Handle(CreateApiTokenCommand request, CancellationToken cancellationToken)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrEmpty(name))
                throw new BadRequestException("Token name is required.");

            // Frontend sends a bare ISO date ("2027-01-01") which JSON model binding
            // produces as Kind=Unspecified. Postgres timestamptz rejects that — coerce
            // to UTC before either validating or persisting.
            DateTime? expiresAtUtc = request.ExpiresAt is { } exp
                ? DateTime.SpecifyKind(exp, DateTimeKind.Utc)
                : null;

            if (expiresAtUtc is { } expUtc && expUtc <= DateTime.UtcNow)
                throw new BadRequestException("ExpiresAt must be in the future.");

            var nameExists = await _context.ApiTokens
                .AsNoTracking()
                .AnyAsync(t => t.UserId == request.UserId && t.Name == name, cancellationToken);
            if (nameExists)
                throw new ConflictException($"An API token named \"{name}\" already exists.");

            var lookup = Prefix + RandomBase32(LookupBytes);
            var secret = RandomBase32(SecretBytes);
            var plaintext = $"{lookup}_{secret}";

            var token = new ApiToken
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Name = name,
                TokenLookup = lookup,
                TokenHash = BCrypt.Net.BCrypt.HashPassword(plaintext),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAtUtc,
            };
            _context.ApiTokens.Add(token);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("user.api_token_created", "ApiToken", token.Id.ToString(),
                new { name, expiresAt = request.ExpiresAt });

            return new CreateApiTokenResult
            {
                Id = token.Id,
                Name = name,
                PlaintextToken = plaintext,
                CreatedAt = token.CreatedAt,
                ExpiresAt = token.ExpiresAt,
            };
        }

        private static string RandomBase32(int byteCount)
        {
            var buf = new byte[byteCount];
            RandomNumberGenerator.Fill(buf);
            return OtpNet.Base32Encoding.ToString(buf).TrimEnd('=');
        }
    }
}
