using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Hosts.Queries
{
    public sealed record GetHostsQuery(ClaimsPrincipal User) : IRequest<IEnumerable<HostDto>>;

    public sealed class GetHostsQueryHandler : IRequestHandler<GetHostsQuery, IEnumerable<HostDto>>
    {
        private readonly IAppDbContext _context;

        public GetHostsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<HostDto>> Handle(GetHostsQuery request, CancellationToken cancellationToken)
        {
            var hosts = await _context.Hosts.ToListAsync(cancellationToken);
            return hosts.Select(h => new HostDto
            {
                Id = h.Id,
                Name = h.Name,
                Address = h.Address
            });
        }
    }
}
