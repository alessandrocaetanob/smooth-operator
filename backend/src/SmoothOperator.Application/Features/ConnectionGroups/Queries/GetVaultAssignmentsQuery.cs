using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.ConnectionGroups.Queries
{
    public sealed record GetVaultAssignmentsQuery(Guid VaultId) : IRequest<VaultAssignmentsDto>;

    public sealed class GetVaultAssignmentsQueryHandler : IRequestHandler<GetVaultAssignmentsQuery, VaultAssignmentsDto>
    {
        private readonly IAppDbContext _context;

        public GetVaultAssignmentsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<VaultAssignmentsDto> Handle(GetVaultAssignmentsQuery request, CancellationToken cancellationToken)
        {
            var vault = await _context.ConnectionGroups
                .AsNoTracking()
                .Include(v => v.Users)
                .Include(v => v.Groups)
                .FirstOrDefaultAsync(v => v.Id == request.VaultId, cancellationToken);

            if (vault == null)
                throw new NotFoundException("Vault not found.");

            return new VaultAssignmentsDto
            {
                UserIds = vault.Users.Select(u => u.Id).ToList(),
                GroupIds = vault.Groups.Select(g => g.Id).ToList()
            };
        }
    }
}
