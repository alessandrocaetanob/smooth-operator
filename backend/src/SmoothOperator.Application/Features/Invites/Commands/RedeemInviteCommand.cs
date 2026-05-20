using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Invites.Commands
{
    public sealed record RedeemInviteCommand(
        string Token,
        string Password,
        string? Name) : IRequest<Unit>;

    public sealed class RedeemInviteCommandHandler : IRequestHandler<RedeemInviteCommand, Unit>
    {
        private readonly IInviteService _invites;
        private readonly IAuditService _audit;

        public RedeemInviteCommandHandler(IInviteService invites, IAuditService audit)
        {
            _invites = invites;
            _audit = audit;
        }

        public async Task<Unit> Handle(RedeemInviteCommand request, CancellationToken cancellationToken)
        {
            var invitation = await _invites.ValidateAsync(request.Token);
            if (invitation == null || invitation.User == null)
                throw new NotFoundException("Invitation is invalid or has expired.");

            var user = invitation.User;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 13);
            if (!string.IsNullOrWhiteSpace(request.Name))
                user.Name = request.Name.Trim();
            user.IsActive = true;

            await _invites.RedeemAsync(request.Token);

            await _audit.WriteAsync("invite.redeemed", "User", user.Id.ToString(),
                new { type = invitation.Type, userId = user.Id });

            return Unit.Value;
        }
    }
}
