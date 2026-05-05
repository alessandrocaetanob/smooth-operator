using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Invites.Queries
{
    public sealed record ValidateInviteQuery(string Token) : IRequest<InvitePreviewDto>;

    public sealed class ValidateInviteQueryHandler : IRequestHandler<ValidateInviteQuery, InvitePreviewDto>
    {
        private readonly IInviteService _invites;

        public ValidateInviteQueryHandler(IInviteService invites) => _invites = invites;

        public async Task<InvitePreviewDto> Handle(ValidateInviteQuery request, CancellationToken cancellationToken)
        {
            var invitation = await _invites.ValidateAsync(request.Token);
            if (invitation == null || invitation.User == null)
                throw new NotFoundException("Invitation is invalid or has expired.");

            return new InvitePreviewDto
            {
                Email = invitation.User.Email,
                Name = invitation.User.Name,
                Type = invitation.Type,
            };
        }
    }
}
