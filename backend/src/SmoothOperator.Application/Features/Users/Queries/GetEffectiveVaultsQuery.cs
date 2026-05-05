using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Users.Queries
{
    public sealed record GetEffectiveVaultsQuery(Guid UserId, ClaimsPrincipal RequestingUser)
        : IRequest<UserEffectiveVaultsDto>;

    public sealed class GetEffectiveVaultsQueryHandler : IRequestHandler<GetEffectiveVaultsQuery, UserEffectiveVaultsDto>
    {
        private readonly IAppDbContext _context;

        public GetEffectiveVaultsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<UserEffectiveVaultsDto> Handle(GetEffectiveVaultsQuery request, CancellationToken cancellationToken)
        {
            var isOwnerOrAdmin = request.RequestingUser.IsInRole("Owner") || request.RequestingUser.IsInRole("Admin");
            var idClaim = request.RequestingUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isSelf = Guid.TryParse(idClaim, out var meId) && meId == request.UserId;

            if (!isSelf && !isOwnerOrAdmin)
                throw new ForbiddenException("Access denied.");

            var userData = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == request.UserId)
                .Select(u => new
                {
                    u.Id,
                    Direct = u.ConnectionGroups
                        .OrderBy(v => v.Name)
                        .Select(v => new EffectiveVaultDto { Id = v.Id, Name = v.Name, ParentGroupId = v.ParentGroupId })
                        .ToList(),
                    ViaGroups = u.Groups
                        .OrderBy(g => g.Name)
                        .Select(g => new EffectiveGroupVaultsDto
                        {
                            GroupId = g.Id,
                            GroupName = g.Name,
                            Vaults = g.Vaults
                                .OrderBy(v => v.Name)
                                .Select(v => new EffectiveVaultDto { Id = v.Id, Name = v.Name, ParentGroupId = v.ParentGroupId })
                                .ToList()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (userData == null)
                throw new NotFoundException("User not found.");

            var merged = userData.Direct
                .Concat(userData.ViaGroups.SelectMany(g => g.Vaults))
                .GroupBy(v => v.Id)
                .Select(grp => grp.First())
                .OrderBy(v => v.Name)
                .ToList();

            return new UserEffectiveVaultsDto
            {
                UserId = userData.Id,
                Direct = userData.Direct,
                ViaGroups = userData.ViaGroups,
                Merged = merged
            };
        }
    }
}
