using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Hosts.Queries
{
    public sealed record GetHostQuery(Guid Id, ClaimsPrincipal User) : IRequest<HostDto>;

    public sealed class GetHostQueryHandler : IRequestHandler<GetHostQuery, HostDto>
    {
        private readonly IAppDbContext _context;

        public GetHostQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<HostDto> Handle(GetHostQuery request, CancellationToken cancellationToken)
        {
            var host = await _context.Hosts
                .FirstOrDefaultAsync(h => h.Id == request.Id, cancellationToken);

            if (host == null)
                throw new NotFoundException("Host not found.");

            return new HostDto
            {
                Id = host.Id,
                Name = host.Name,
                Address = host.Address
            };
        }
    }
}
