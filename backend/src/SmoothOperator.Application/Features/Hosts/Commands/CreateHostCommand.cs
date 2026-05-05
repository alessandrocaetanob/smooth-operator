using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Hosts.Commands
{
    public sealed record CreateHostCommand(CreateHostDto Dto, ClaimsPrincipal User) : IRequest<HostDto>;

    public sealed class CreateHostCommandHandler : IRequestHandler<CreateHostCommand, HostDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public CreateHostCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<HostDto> Handle(CreateHostCommand request, CancellationToken cancellationToken)
        {
            var host = new SmoothOperator.Domain.Models.Host
            {
                Id = Guid.NewGuid(),
                Name = request.Dto.Name,
                Address = request.Dto.Address
            };

            _context.Hosts.Add(host);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("host.created", "Host", host.Id.ToString(),
                new { host.Name, host.Address });

            return new HostDto
            {
                Id = host.Id,
                Name = host.Name,
                Address = host.Address
            };
        }
    }
}
