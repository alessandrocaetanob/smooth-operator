using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.Users.Queries
{
    public sealed record GetUsersQuery(ClaimsPrincipal User) : IRequest<IEnumerable<UserListItemDto>>;

    public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IEnumerable<UserListItemDto>>
    {
        private readonly IAppDbContext _context;

        public GetUsersQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserListItemDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.ConnectionGroups)
                .OrderBy(u => u.Email)
                .ToListAsync(cancellationToken);

            return users.Select(Project);
        }

        internal static UserListItemDto Project(User user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            IsActive = user.IsActive,
            SsoLinked = !string.IsNullOrEmpty(user.ExternalId),
            SsoProviderType = user.SsoProviderType?.ToString(),
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            CreatedAt = user.CreatedAt,
            Roles = user.Roles.Select(r => r.Name).OrderBy(r => r).ToList(),
            VaultIds = user.ConnectionGroups.Select(g => g.Id).OrderBy(v => v).ToList()
        };
    }
}
