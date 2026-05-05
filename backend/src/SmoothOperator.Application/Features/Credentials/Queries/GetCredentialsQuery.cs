using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Credentials.Queries
{
    public sealed record GetCredentialsQuery(string? VaultId, ClaimsPrincipal User)
        : IRequest<IEnumerable<CredentialDto>>;

    public sealed class GetCredentialsQueryHandler : IRequestHandler<GetCredentialsQuery, IEnumerable<CredentialDto>>
    {
        private readonly IAppDbContext _context;

        public GetCredentialsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CredentialDto>> Handle(GetCredentialsQuery request, CancellationToken cancellationToken)
        {
            var credentials = await _context.Credentials.AsNoTracking().ToListAsync(cancellationToken);
            return credentials.Select(c => new CredentialDto
            {
                Id = c.Id,
                Name = c.Name,
                Username = c.Username,
                CredentialType = c.CredentialType,
                PublicKey = c.PublicKey
            });
        }
    }
}
