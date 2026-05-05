using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Users.Queries
{
    public sealed record GetUserVaultAssignmentsQuery(Guid UserId, ClaimsPrincipal User) : IRequest<IEnumerable<Guid>>;

    public sealed class GetUserVaultAssignmentsQueryHandler : IRequestHandler<GetUserVaultAssignmentsQuery, IEnumerable<Guid>>
    {
        private readonly IAppDbContext _context;

        public GetUserVaultAssignmentsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Guid>> Handle(GetUserVaultAssignmentsQuery request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.ConnectionGroups)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            return user.ConnectionGroups.Select(g => g.Id).OrderBy(v => v).ToList();
        }
    }
}
