using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using SmoothOperator.Domain.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmoothOperator.Application.Options;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Infrastructure.Services
{
    public class TokenService : ITokenService
    {
        public const string LocalIssuer = "smooth-operator";
        public const string LocalAudience = "smooth-operator-api";

        private readonly SymmetricSecurityKey _signingKey;
        private readonly string _issuer;
        private readonly string _audience;

        public TimeSpan TokenLifetime { get; } = TimeSpan.FromHours(8);

        public TokenService(IOptions<JwtOptions> options)
        {
            var cfg = options.Value;

            if (Encoding.UTF8.GetByteCount(cfg.Key) < 32)
                throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long for HS256.");

            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.Key));
            _issuer = cfg.Issuer;
            _audience = cfg.Audience;
        }

        public string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name),
            };

            foreach (var roleName in user.Roles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, roleName));
            }

            var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.Add(TokenLifetime),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
