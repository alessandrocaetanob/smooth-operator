using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Hosts.Commands
{
    public sealed record DeleteHostCommand(Guid Id, ClaimsPrincipal User) : IRequest<bool>;

    public sealed class DeleteHostCommandHandler : IRequestHandler<DeleteHostCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;

        public DeleteHostCommandHandler(IAppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<bool> Handle(DeleteHostCommand request, CancellationToken cancellationToken)
        {
            var host = await _context.Hosts
                .FirstOrDefaultAsync(h => h.Id == request.Id, cancellationToken);
            if (host == null)
                throw new NotFoundException("Host not found.");

            _context.Hosts.Remove(host);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("host.deleted", "Host", request.Id.ToString(),
                new { host.Name });

            return true;
        }
    }
}
