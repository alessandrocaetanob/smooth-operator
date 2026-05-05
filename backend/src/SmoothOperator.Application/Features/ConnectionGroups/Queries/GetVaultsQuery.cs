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

namespace SmoothOperator.Application.Features.ConnectionGroups.Queries
{
    public sealed record GetVaultsQuery(ClaimsPrincipal User) : IRequest<IEnumerable<ConnectionGroupDto>>;

    public sealed class GetVaultsQueryHandler : IRequestHandler<GetVaultsQuery, IEnumerable<ConnectionGroupDto>>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public GetVaultsQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<IEnumerable<ConnectionGroupDto>> Handle(GetVaultsQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            return await _access.ApplyVaultScope(_context.ConnectionGroups.AsNoTracking(), profile)
                .OrderBy(v => v.Name)
                .Select(v => new ConnectionGroupDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    ParentGroupId = v.ParentGroupId,
                    UserCount = v.Users.Count,
                    GroupCount = v.Groups.Count
                })
                .ToListAsync(cancellationToken);
        }
    }
}
