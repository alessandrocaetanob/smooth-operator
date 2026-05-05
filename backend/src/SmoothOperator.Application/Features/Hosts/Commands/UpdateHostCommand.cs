using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Hosts.Commands
{
    public sealed record UpdateHostCommand(Guid Id, CreateHostDto Dto, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class UpdateHostCommandHandler : IRequestHandler<UpdateHostCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public UpdateHostCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(UpdateHostCommand request, CancellationToken cancellationToken)
        {
            var host = await _context.Hosts
                .FirstOrDefaultAsync(h => h.Id == request.Id, cancellationToken);
            if (host == null)
                throw new NotFoundException("Host not found.");

            host.Name = request.Dto.Name;
            host.Address = request.Dto.Address;

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("host.updated", "Host", request.Id.ToString(),
                new { host.Name, host.Address });

            return true;
        }
    }
}
